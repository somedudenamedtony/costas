// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Integrations.PskReporter;
using MQTTnet;
using MQTTnet.Server;

namespace Ft8Client.Integrations.Tests;

public class PskReporterTests
{
    private static readonly CountryFile Countries = ReferenceData.LoadCountries();

    // Reference built by an independent implementation of the WSJT-X layout (Python, from PSKReporterIPFIX.cpp).
    private const string ReferenceHex =
        "000a00c46959d68400000000123456780002003c50e300078001ffff0000768f800500050000768f800600010000768f800affff0000768f8003ffff0000768f" +
        "800b00010000768f009600040003003450e2000500018002ffff0000768f8004ffff0000768f8008ffff0000768f8009ffff0000768f800dffff0000768f0000" +
        "50e200240557374c495404444e34301046543820436c69656e7420302e312e300000000050e30020054b314142430000d6c478f60346543804464e3432016959d6480000";

    [Fact]
    public void Build_OneSpotWithDescriptors_MatchesReferenceByteForByte()
    {
        var receiver = new PskReceiver("W7LIT", "DN40", "FT8 Client 0.1.0");
        var spot = new PskSpot("K1ABC", 14_075_000, -10, "FT8", "FN42", DateTimeOffset.FromUnixTimeSeconds(1767495240).UtcDateTime);
        var p = IpfixPacketBuilder.Build(receiver, [spot], includeDescriptors: true, sequence: 0, observationId: 0x12345678, exportTime: 1767495300);
        p.Should().ContainSingle();
        Convert.ToHexStringLower(p[0].Payload).Should().Be(ReferenceHex);
        p[0].SpotCount.Should().Be(1);
    }

    [Fact]
    public void Build_ManySpots_SplitsUnderUdpLimitAndPadsSets()
    {
        var receiver = new PskReceiver("W7LIT", "DN40", "FT8 Client 0.1.0");
        var spots = Enumerable.Range(0, 120).Select(i => new PskSpot($"K{i % 10}AB{(char)('A' + i % 26)}", 14_075_000 + i, -10, "FT8", "FN42", DateTime.UtcNow)).ToList();
        var packets = IpfixPacketBuilder.Build(receiver, spots, true, 100, 1, 2);
        packets.Should().HaveCountGreaterThan(1);
        packets.Sum(x => x.SpotCount).Should().Be(120);
        packets.Should().OnlyContain(x => x.Payload.Length <= IpfixPacketBuilder.MaxUdpPayload && x.Payload.Length % 4 == 0);
        // Descriptors only in the first packet of a batch.
        packets.Skip(1).Should().OnlyContain(x => x.Payload[16] == 0x50 && x.Payload[17] == 0xE2);
        // The IPFIX header carries the running sequence number.
        BitConverter.ToUInt32(packets[1].Payload.AsSpan(8, 4).ToArray().Reverse().ToArray()).Should().Be(100u + (uint)packets[0].SpotCount);
    }

    [Fact]
    public async Task Uploader_RespectsRules()
    {
        var clock = new ManualClock(new DateTime(2026, 1, 4, 3, 0, 0, DateTimeKind.Utc));
        var sent = new List<byte[]>();
        var up = new PskSpotUploader(b =>
        {
            sent.Add(b);
            return Task.CompletedTask;
        }, clock, new Random(1)) { Receiver = new PskReceiver("W7LIT", "DN40", "FT8 Client") };
        PskSpot S(string call, int minutes) => new(call, 14_075_000, -10, "FT8", "FN42", clock.UtcNow.AddMinutes(minutes));

        up.Add(S("W7LIT", 0)).Should().BeFalse("never spot my own call");
        up.Add(S("<...>", 0)).Should().BeFalse();
        up.Add(S("K1ABC", 0)).Should().BeTrue();
        up.Add(S("K1ABC", 1)).Should().BeFalse("each call at most once per five minutes");
        (await up.TickAsync()).Should().Be(0, "not before five minutes");
        clock.Advance(TimeSpan.FromMinutes(5.6));
        (await up.TickAsync()).Should().Be(1);
        up.Add(S("K1ABC", 0)).Should().BeTrue();
        sent[0][16].Should().Be(0x00, "the first packets carry the templates (set id 2)");
        sent[0][17].Should().Be(0x02);
        for (var i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(5.6));
            up.Add(S($"K{i}XYZ", 0));
            await up.TickAsync();
        }
        sent.Should().HaveCount(4);
        sent[3][17].Should().Be(0xE2, "templates only in the first three packets until the hour is up");
    }

    [Fact]
    public void ParseQuery_Sample_ReadsReportsAndSequence()
    {
        var (reports, last) = PskReportParser.ParseQuery(File.ReadAllText(Path.Combine(TestPaths.Samples, "pskreporter", "query-reply.xml")), Countries);
        last.Should().Be(73380253972);
        reports.Should().HaveCount(2, "a report without sNR is skipped");
        reports[0].Should().BeEquivalentTo(new ReceptionReport("ZL2RPA", "RF70", "ZL", null, "20m", "FT8", -22,
            DateTimeOffset.FromUnixTimeSeconds(1767495240).UtcDateTime, 14075432));
    }

    [Fact]
    public async Task Feed_LocalBroker_ReceivesFiltersDedupesAndReconnects()
    {
        var port = FreePort();
        var factory = new MqttServerFactory();
        var server = factory.CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        var received = new List<ReceptionReport>();
        await using var feed = new PskFeedClient(Countries, "127.0.0.1", port, tls: false);
        feed.Report += r =>
        {
            lock (received) received.Add(r);
        };
        feed.Start("W7LIT");
        await WaitFor(() => feed.Connected);

        await Publish(server);
        await WaitFor(() => received.Count >= 3);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        lock (received)
        {
            received.Select(r => r.ReceiverCall).Should().Equal("ZL2RPA", "JE1XYZ", "KG5OWB");
            received[0].ReceiverEntityKey.Should().Be("ZL");
            received[2].Band.Should().Be("40m");
        }

        // Broker restarts: the client reconnects by itself.
        await server.StopAsync(new MqttServerStopOptions());
        await WaitFor(() => !feed.Connected);
        server = factory.CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        await WaitFor(() => feed.Connected, 30);
        feed.Connections.Should().Be(2);
        await server.StopAsync(new MqttServerStopOptions());
    }

    private static async Task Publish(MqttServer server)
    {
        foreach (var line in File.ReadAllLines(Path.Combine(TestPaths.Samples, "pskreporter", "mqtt-messages.jsonl")))
        {
            var sc = line.Contains("\"sc\":\"K1ABC\"", StringComparison.Ordinal) ? "K1ABC" : "W7LIT";
            var msg = new MqttApplicationMessageBuilder().WithTopic($"pskr/filter/v2/20m/FT8/{sc}/RX/DN40/FN42/291/1").WithPayload(line).Build();
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg) { SenderClientId = "test" });
        }
    }

    private static async Task WaitFor(Func<bool> cond, int seconds = 10)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!cond())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Condition not met.");
            await Task.Delay(50);
        }
    }

    private static MqttServerOptions BrokerOptions(int port) => new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
        .WithDefaultEndpointBoundIPV6Address(IPAddress.None)
        .WithDefaultEndpointPort(port)
        .Build();

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
