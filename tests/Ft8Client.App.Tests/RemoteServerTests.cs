// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ft8Client.App.Engine;
using Ft8Client.App.Remote;
using Ft8Client.Core;
using Ft8Client.Core.Remote;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

public sealed class RemoteServerTests : IAsyncLifetime
{
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 20, 31, 0, DateTimeKind.Utc));
    private readonly FakeRemoteTarget _target = new()
    {
        Snapshot = new SessionSnapshot { MyCall = "W7LIT", Band = "20m", Mode = Mode.Ft8, DialHz = 14_074_000 },
    };
    private readonly HttpClient _http = new();
    private RemoteControl _control = null!;
    private RemoteServer _server = null!;
    private int _port;

    public async ValueTask InitializeAsync()
    {
        _control = new RemoteControl(_target, _clock, new DeviceTokens(), _ => { }, new PairingGate(() => 271828));
        _server = new RemoteServer(_control, id => RemoteStateBuilder.Build(_target.Snapshot, _clock.UtcNow, new RemoteContext { Units = DistanceUnit.Miles },
            _control.ControlFor(id), _control.ViewingFor(id)));
        _port = FreePort();
        await _server.StartAsync(_port);
        _http.BaseAddress = new Uri($"http://127.0.0.1:{_port}/");
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task Get_Root_ServesThePageWithSecurityHeaders()
    {
        var r = await _http.GetAsync("/", TestContext.Current.CancellationToken);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Costas remote");
        r.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
    }

    [Fact]
    public async Task Pair_WrongCode_Forbidden()
    {
        _control.Pairing.Open(_clock.UtcNow);
        var r = await _http.PostAsJsonAsync("/api/pair", new { code = "000000" }, TestContext.Current.CancellationToken);
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Socket_PairedPhone_GetsStateAndCommandsReachTheSession()
    {
        var token = await PairAsync();
        using var ws = await ConnectAsync();
        await SendAsync(ws, new { type = "hello", token });

        var first = await ReceiveAsync(ws);
        first.GetProperty("type").GetString().Should().Be("state");
        first.GetProperty("state").GetProperty("myCall").GetString().Should().Be("W7LIT");
        first.GetProperty("state").GetProperty("frequency").GetString().Should().Be("14.074 · 20 m FT8");
        first.GetProperty("state").GetProperty("control").GetString().Should().Be("desk");

        await SendAsync(ws, new { type = "call", call = "VE7XYZ" });
        var after = await ReceiveAsync(ws);
        after.GetProperty("state").GetProperty("control").GetString().Should().Be("you");
        _target.Calls.Should().Equal("call VE7XYZ");
    }

    [Fact]
    public async Task Socket_Show_SendsThatStationsDetailsWithoutTakingControl()
    {
        var token = await PairAsync();
        using var ws = await ConnectAsync();
        await SendAsync(ws, new { type = "hello", token });
        var first = await ReceiveAsync(ws);
        first.GetProperty("state").GetProperty("details").ValueKind.Should().Be(JsonValueKind.Null);

        await SendAsync(ws, new { type = "show", call = "ve7xyz" });
        var after = await ReceiveAsync(ws);
        after.GetProperty("state").GetProperty("details").GetProperty("call").GetString().Should().Be("VE7XYZ");
        after.GetProperty("state").GetProperty("control").GetString().Should().Be("desk");
        _target.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Socket_UnpairedToken_ClosedAndNothingReachesTheSession()
    {
        using var ws = await ConnectAsync();
        await SendAsync(ws, new { type = "hello", token = "guess" });
        var buf = new byte[256];
        var r = await ws.ReceiveAsync(buf, TestContext.Current.CancellationToken);
        r.MessageType.Should().Be(WebSocketMessageType.Close);
        ws.CloseStatusDescription.Should().Be("unpaired");
        _target.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Socket_OtherSiteOrigin_Refused()
    {
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", "http://evil.example");
        var act = () => ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_port}/ws"), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<WebSocketException>();
    }

    private async Task<string> PairAsync()
    {
        _control.Pairing.Open(_clock.UtcNow);
        var r = await _http.PostAsJsonAsync("/api/pair", new { code = "271828" }, TestContext.Current.CancellationToken);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var j = await r.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return j.GetProperty("token").GetString()!;
    }

    private async Task<ClientWebSocket> ConnectAsync()
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_port}/ws"), TestContext.Current.CancellationToken);
        return ws;
    }

    private static Task SendAsync(ClientWebSocket ws, object message) =>
        ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);

    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket ws)
    {
        var buf = new byte[64 * 1024];
        var count = 0;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf, count, buf.Length - count), cts.Token);
            count += r.Count;
            if (r.EndOfMessage) return JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, count)).RootElement.Clone();
        }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
