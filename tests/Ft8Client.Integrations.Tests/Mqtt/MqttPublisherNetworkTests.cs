// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Mqtt;
using Ft8Client.Core.Services;
using Ft8Client.Integrations.Mqtt;

namespace Ft8Client.Integrations.Tests.Mqtt;

/// <summary>
/// The publisher against network trouble: a dropped connection, a frozen link, a broker that is down or refuses the
/// password, and a connection left idle for longer than the keep-alive. A <see cref="TcpProxy"/> sits between the
/// publisher and the broker to cause the trouble.
/// </summary>
public class MqttPublisherNetworkTests
{
    private static readonly MqttTopics Topics = new("w7lit");

    private static MqttBrokerOptions Options(int port, string? user = null, string? password = null) =>
        new("127.0.0.1", port, false, user, password, $"costas-test-{Guid.NewGuid():N}"[..23]);

    /// <summary>Records every status the publisher raises.</summary>
    private sealed class Statuses
    {
        private readonly object _gate = new();
        private readonly List<ServiceStatus> _all = [];

        public void Add(ServiceStatus s)
        {
            lock (_gate) _all.Add(s);
        }

        public IReadOnlyList<ServiceStatus> All
        {
            get { lock (_gate) return [.. _all]; }
        }

        public async Task<ServiceStatus> WaitForAsync(Func<ServiceStatus, bool> match, int after = 0)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                var found = All.Skip(after).FirstOrDefault(match);
                if (found is not null) return found;
                await Task.Delay(20);
            }
            throw new TimeoutException("The expected status was not raised within 30 s. Seen: "
                                       + string.Join(" | ", All.Select(s => $"{s.Health} {s.Message}")));
        }
    }

    private static async Task Until(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(20);
        }
        (await condition()).Should().BeTrue(because);
    }

    private static Task Until(Func<bool> condition, string because) => Until(() => Task.FromResult(condition()), because);

    [Fact]
    public async Task Connected_IdleLongerThanTheKeepAlive_StaysConnected()
    {
        await using var broker = await MqttTestBroker.StartAsync();
        await using var proxy = new TcpProxy(broker.Port);
        await using var p = new MqttPublisher(Options(proxy.Port), Topics, () => []);
        p.Start();
        await broker.WaitForAsync(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online);

        // Nothing to publish for more than twice the keep-alive: the client's pings must keep the connection, or the
        // broker would drop it and announce Costas offline every few seconds on a quiet band.
        await Task.Delay(MqttBrokerOptions.KeepAlive * 2.5, TestContext.Current.CancellationToken);

        proxy.Accepted.Should().Be(1, "the first connection is still the one in use");
        p.Connected.Should().BeTrue();
        broker.Published.Should().NotContain(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Offline);
        (await broker.ConnectedClientsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ConnectionCut_WithoutGoodbye_BrokerSendsTheLastWillOffline()
    {
        await using var broker = await MqttTestBroker.StartAsync();
        await using var proxy = new TcpProxy(broker.Port);
        await using var p = new MqttPublisher(Options(proxy.Port), Topics, () => []);
        p.Start();
        await Until(async () => (await broker.RetainedAsync()).GetValueOrDefault(Topics.Availability) == MqttPayloads.Online, "Costas says online");

        // A crash or a pulled cable: the connection drops with no MQTT goodbye, and Costas cannot get back in.
        proxy.Target = MqttTestBroker.FreePort();
        proxy.Cut();

        await Until(async () => (await broker.RetainedAsync()).GetValueOrDefault(Topics.Availability) == MqttPayloads.Offline,
            "the broker publishes Costas's last will, so Home Assistant shows it offline");
    }

    [Fact]
    public async Task ConnectionCut_Reconnects_PublishesTheCurrentStateBeforeOnline()
    {
        await using var broker = await MqttTestBroker.StartAsync();
        await using var proxy = new TcpProxy(broker.Port);
        var session = "{\"state\":\"on\"}";
        var statuses = new Statuses();
        await using var p = new MqttPublisher(Options(proxy.Port), Topics, () => [new(Topics.Session, Volatile.Read(ref session), true)]);
        p.StatusChanged += statuses.Add;
        p.Start();
        await broker.WaitForAsync(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online);
        await statuses.WaitForAsync(s => s.Health == ServiceHealth.Ok);

        Volatile.Write(ref session, "{\"state\":\"off\",\"reason\":\"idle_timeout\"}");
        p.Publish(new MqttOutgoing(Topics.SessionEvent, "{\"event_type\":\"session_ended\"}", false)); // may or may not make it
        var before = broker.Published.Count;
        proxy.Cut();

        await statuses.WaitForAsync(s => s.Health != ServiceHealth.Ok);
        await Until(() => proxy.Accepted >= 2, "Costas reconnects by itself");
        // "online" on the new connection. The broker's last will ("offline") also lands after the cut, so look only at
        // what came after it, or the first connection's "online" would match before Costas is back.
        await Until(() => broker.Published.Skip(before).Any(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online),
            "Costas says online again");
        var after = broker.Published.Skip(before).ToList();
        var state = after.FindIndex(m => m.Topic == Topics.Session && m.Payload.Contains("idle_timeout", StringComparison.Ordinal));
        state.Should().BeGreaterThanOrEqualTo(0, "the state as it is now is sent on the new connection");
        state.Should().BeLessThan(after.FindIndex(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online),
            "state first, so nothing reads a stale session once Costas says online");
        await statuses.WaitForAsync(s => s.Health == ServiceHealth.Ok, after: 1);
        (await broker.RetainedAsync())[Topics.Session].Should().Contain("idle_timeout");
    }

    [Fact]
    public async Task LinkFreezes_ConnectionGivenUp_ReconnectsWhenItRecovers()
    {
        await using var broker = await MqttTestBroker.StartAsync();
        await using var proxy = new TcpProxy(broker.Port);
        var statuses = new Statuses();
        await using var p = new MqttPublisher(Options(proxy.Port), Topics, () => []);
        p.StatusChanged += statuses.Add;
        p.Start();
        await statuses.WaitForAsync(s => s.Health == ServiceHealth.Ok);

        // The Wi-Fi dies without closing anything: no bytes either way. The keep-alive must notice.
        proxy.Freeze(true);
        await statuses.WaitForAsync(s => s.Health != ServiceHealth.Ok);
        p.Connected.Should().BeFalse();
        await Until(async () => (await broker.RetainedAsync()).GetValueOrDefault(Topics.Availability) == MqttPayloads.Offline,
            "the broker gives up on the silent client and publishes its last will");

        proxy.Freeze(false);
        proxy.Cut(); // the dead link's sockets finally close

        var seen = statuses.All.Count;
        await statuses.WaitForAsync(s => s.Health == ServiceHealth.Ok, after: seen);
        await Until(async () => (await broker.RetainedAsync()).GetValueOrDefault(Topics.Availability) == MqttPayloads.Online, "online again");
    }

    [Fact]
    public async Task BrokerDown_SaysSoAndRetries_ConnectsOnceItIsUp()
    {
        var statuses = new Statuses();
        var port = MqttTestBroker.FreePort();
        await using var p = new MqttPublisher(Options(port), Topics, () => [new(Topics.Tx, "OFF", true)]);
        p.StatusChanged += statuses.Add;
        p.Start();

        var down = await statuses.WaitForAsync(s => s.Health == ServiceHealth.Down);
        down.Message.Should().Be($"Nothing answered at 127.0.0.1 port {port}. Is the broker running?");
        await statuses.WaitForAsync(s => s.Health == ServiceHealth.Down, after: statuses.All.ToList().IndexOf(down) + 1);
        p.Connected.Should().BeFalse("it keeps trying, and is not connected yet");

        await using var broker = await MqttTestBroker.StartAsync(port: port);

        await broker.WaitForAsync(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online);
        await statuses.WaitForAsync(s => s.Health == ServiceHealth.Ok);
        p.Connected.Should().BeTrue();
        (await broker.RetainedAsync())[Topics.Tx].Should().Be("OFF");
    }

    [Fact]
    public async Task WrongPassword_StatusSaysSo_PublishesNothing()
    {
        await using var broker = await MqttTestBroker.StartAsync("tony", "secret");
        var statuses = new Statuses();
        await using var p = new MqttPublisher(Options(broker.Port, "tony", "wrong"), Topics, () => [new(Topics.Tx, "OFF", true)]);
        p.StatusChanged += statuses.Add;
        p.Start();

        var down = await statuses.WaitForAsync(s => s.Health == ServiceHealth.Down);

        down.Message.Should().Be("The broker refused the user name or password.");
        p.Connected.Should().BeFalse();
        broker.Published.Should().BeEmpty();
        statuses.All.Should().NotContain(s => s.Health == ServiceHealth.Ok);
    }

    [Fact]
    public async Task StopAsync_BrokerUnreachable_ReturnsPromptlyWithoutThrowing()
    {
        await using var p = new MqttPublisher(Options(MqttTestBroker.FreePort()), Topics, () => []);
        p.Start();

        var stop = p.StopAsync([new(Topics.Session, "{}", true)], TimeSpan.FromSeconds(2));

        (await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken))).Should().BeSameAs(stop,
            "closing Costas must not wait on a broker that is not there");
        await stop;
        p.Connected.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_LinkFrozen_GivesUpAfterTheTimeout()
    {
        await using var broker = await MqttTestBroker.StartAsync();
        await using var proxy = new TcpProxy(broker.Port);
        await using var p = new MqttPublisher(Options(proxy.Port), Topics, () => []);
        p.Start();
        await broker.WaitForAsync(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online);
        proxy.Freeze(true);

        var stop = p.StopAsync([new(Topics.Session, "{}", true)], TimeSpan.FromSeconds(2));

        (await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken))).Should().BeSameAs(stop,
            "a final message the broker never acknowledges must not hold up closing");
        await stop;
    }

    [Fact]
    public async Task Publish_AfterDispose_DoesNotThrow()
    {
        var p = new MqttPublisher(Options(MqttTestBroker.FreePort()), Topics, () => []);
        p.Start();
        await p.DisposeAsync();

        var publish = () =>
        {
            p.Publish(new MqttOutgoing(Topics.Tx, "ON", true));
            p.Publish(new MqttOutgoing(Topics.SessionEvent, "{}", false));
        };

        publish.Should().NotThrow("the transmitter's PTT events publish, and must never fail because MQTT has stopped");
    }

    [Fact]
    public async Task StopAsync_NeverStarted_DoesNothing()
    {
        await using var p = new MqttPublisher(Options(MqttTestBroker.FreePort()), Topics, () => []);

        await p.StopAsync([new(Topics.Session, "{}", true)], TimeSpan.FromSeconds(1));

        p.Connected.Should().BeFalse();
        p.LastPublishUtc.Should().BeNull();
    }

    [Fact]
    public async Task Start_Twice_OneConnection()
    {
        await using var broker = await MqttTestBroker.StartAsync();
        await using var proxy = new TcpProxy(broker.Port);
        await using var p = new MqttPublisher(Options(proxy.Port), Topics, () => []);

        p.Start();
        p.Start();
        await broker.WaitForAsync(m => m.Topic == Topics.Availability && m.Payload == MqttPayloads.Online);

        proxy.Accepted.Should().Be(1);
        p.LastPublishUtc.Should().NotBeNull();
    }
}
