// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Neighbours;
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

    [Fact]
    public async Task Feed_RememberedDxcc_UsedUntilMySignalGivesOne()
    {
        var port = FreePort();
        var server = new MqttServerFactory().CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        await using var feed = new PskFeedClient(Countries, "127.0.0.1", port, tls: false);

        feed.RememberMyDxcc(1);
        feed.MyDxcc.Should().Be(1);
        feed.Start("W7LIT");
        await WaitFor(() => feed.Connected);
        await Publish(server);

        await WaitFor(() => feed.MyDxcc == 291);
        await server.StopAsync(new MqttServerStopOptions());
    }

    [Fact]
    public async Task Feed_FollowNeighbours_RaisesNeighbourReportsAndLearnsMyDxcc()
    {
        var port = FreePort();
        var server = new MqttServerFactory().CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        var neighbours = new List<NeighbourReport>();
        await using var feed = new PskFeedClient(Countries, "127.0.0.1", port, tls: false);
        feed.NeighbourReport += r =>
        {
            lock (neighbours) neighbours.Add(r);
        };
        feed.Start("W7LIT");
        await WaitFor(() => feed.Connected);
        await Publish(server);
        await WaitFor(() => feed.MyDxcc is not null);
        feed.MyDxcc.Should().Be(291);

        feed.FollowNeighbours("20m", "FT8", 291);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        var line = """{"sq":1,"f":14075500,"md":"FT8","rp":-12,"t":1767495300,"sc":"K7AAA","sl":"DN41","rc":"DL1ABC","rl":"JO62","sa":291,"ra":230,"b":"20m"}""";
        var msg = new MqttApplicationMessageBuilder().WithTopic("pskr/filter/v2/20m/FT8/K7AAA/DL1ABC/DN41/JO62/291/230").WithPayload(line).Build();
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg) { SenderClientId = "test" }, TestContext.Current.CancellationToken);
        await WaitFor(() =>
        {
            lock (neighbours) return neighbours.Count > 0;
        });
        lock (neighbours)
        {
            neighbours.Should().ContainSingle().Which.Should().Be(new NeighbourReport("K7AAA", "DN41", "DL1ABC", "20m", "FT8", -12,
                DateTimeOffset.FromUnixTimeSeconds(1767495300).UtcDateTime));
        }
        await server.StopAsync(new MqttServerStopOptions());
    }

    [Fact]
    public async Task Feed_AllBandsTooBusy_SamplesTheOtherBands()
    {
        var port = FreePort();
        var server = new MqttServerFactory().CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        var neighbours = new List<NeighbourReport>();
        var samples = new List<BandSample>();
        await using var feed = new PskFeedClient(Countries, "127.0.0.1", port, tls: false, sampleDwell: TimeSpan.FromSeconds(2));
        feed.NeighbourReport += r =>
        {
            lock (neighbours) neighbours.Add(r);
        };
        feed.BandSampled += x =>
        {
            lock (samples) samples.Add(x);
        };
        feed.RememberMyDxcc(291);
        feed.Start("W7LIT");
        await WaitFor(() => feed.Connected);
        feed.FollowNeighbours("20m", "FT8", 291, allBands: true, bands: ["20m", "15m"]);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        async Task Send(long sq, string band, string call)
        {
            var json = $$"""{"sq":{{sq}},"f":14075500,"md":"FT8","rp":-12,"t":1767495300,"sc":"{{call}}","sl":"DN41","rc":"DL1ABC","rl":"JO62","sa":291,"ra":230,"b":"{{band}}"}""";
            var msg = new MqttApplicationMessageBuilder().WithTopic($"pskr/filter/v2/{band}/FT8/{call}/DL1ABC/DN41/JO62/291/230").WithPayload(json).Build();
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg) { SenderClientId = "test" }, TestContext.Current.CancellationToken);
        }
        // More than the limit in one minute (retried across a minute boundary).
        long seq = 1000;
        while (!feed.AllBandsStopped && seq < 30000) await Send(seq++, "20m", "K7AAA");
        feed.AllBandsStopped.Should().BeTrue();

        // 15 m takes its turn; what arrives then is a sample, not a neighbour report.
        int before;
        lock (neighbours) before = neighbours.Count;
        for (var i = 0; i < 100; i++)
        {
            lock (samples)
            {
                if (samples.Any(x => x.Reports.Count > 0)) break;
            }
            await Send(i + 1, "15m", "N7BBB");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
        lock (samples)
        {
            var sample = samples.First(x => x.Reports.Count > 0);
            sample.Band.Should().Be("15m");
            sample.Mode.Should().Be("FT8");
            sample.Reports.Should().OnlyContain(r => r.SenderCall == "N7BBB");
        }
        lock (neighbours) neighbours.Skip(before).Should().NotContain(r => r.Band == "15m");
        await server.StopAsync(new MqttServerStopOptions());
    }

    [Fact]
    public async Task Feed_FollowHunt_RaisesReportsOfHuntedCallsAndCountries()
    {
        var port = FreePort();
        var server = new MqttServerFactory().CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        var spots = new List<HuntSpot>();
        await using var feed = new PskFeedClient(Countries, "127.0.0.1", port, tls: false);
        feed.HuntReport += r =>
        {
            lock (spots) spots.Add(r);
        };
        feed.Start("W7LIT");
        await WaitFor(() => feed.Connected);
        feed.FollowHunt(["VK9XA"], [339], 291);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        async Task Send(string topic, string json)
        {
            var msg = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(json).Build();
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg) { SenderClientId = "test" }, TestContext.Current.CancellationToken);
        }
        await Send("pskr/filter/v2/17m/FT8/VK9XA/K7ABC/OH29/DN31/35/291",
            """{"sq":11,"f":18101000,"md":"FT8","rp":-14,"t":1767495300,"sc":"VK9XA","sl":"OH29","rc":"K7ABC","rl":"DN31","sa":35,"ra":291,"b":"17m"}""");
        await Send("pskr/filter/v2/20m/FT8/JA1ABC/W7XYZ/PM95/DN40/339/291",
            """{"sq":12,"f":14075000,"md":"FT8","rp":-10,"t":1767495300,"sc":"JA1ABC","sl":"PM95","rc":"W7XYZ","rl":"DN40","sa":339,"ra":291,"b":"20m"}""");
        await WaitFor(() =>
        {
            lock (spots) return spots.Count >= 2;
        });
        lock (spots)
        {
            spots.Select(x => x.SenderCall).Should().Equal("VK9XA", "JA1ABC");
            spots[0].Report.ReceiverGrid.Should().Be("DN31");
            spots[1].SenderDxcc.Should().Be(339);
        }
        await server.StopAsync(new MqttServerStopOptions());
    }

    [Fact]
    public void HuntTopics_CallAndEntity_FollowTheTopicLayout()
    {
        PskFeedClient.HuntCallTopicFor("vk9xa").Should().Be("pskr/filter/v2/+/+/VK9XA/#");
        PskFeedClient.HuntEntityTopicFor(35, 291).Should().Be("pskr/filter/v2/+/+/+/+/+/+/35/291/#");
        PskFeedClient.HuntEntityTopicFor(35, null).Should().Be("pskr/filter/v2/+/+/+/+/+/+/35/+/#");
    }

    [Fact]
    public async Task Feed_FollowNeighboursOnAllBands_RaisesReportsFromOtherBands()
    {
        var port = FreePort();
        var server = new MqttServerFactory().CreateMqttServer(BrokerOptions(port));
        await server.StartAsync();
        var neighbours = new List<NeighbourReport>();
        await using var feed = new PskFeedClient(Countries, "127.0.0.1", port, tls: false);
        feed.NeighbourReport += r =>
        {
            lock (neighbours) neighbours.Add(r);
        };
        feed.Start("W7LIT");
        await WaitFor(() => feed.Connected);

        feed.FollowNeighbours("20m", "FT8", 291, allBands: true);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        var line = """{"sq":2,"f":21075500,"md":"FT8","rp":-15,"t":1767495300,"sc":"K7AAA","sl":"DN41","rc":"PY2ABC","rl":"GG66","sa":291,"ra":108,"b":"15m"}""";
        var msg = new MqttApplicationMessageBuilder().WithTopic("pskr/filter/v2/15m/FT8/K7AAA/PY2ABC/DN41/GG66/291/108").WithPayload(line).Build();
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg) { SenderClientId = "test" }, TestContext.Current.CancellationToken);
        await WaitFor(() =>
        {
            lock (neighbours) return neighbours.Count > 0;
        });
        lock (neighbours) neighbours.Should().ContainSingle().Which.Band.Should().Be("15m");
        feed.AllBandsStopped.Should().BeFalse();
        await server.StopAsync(new MqttServerStopOptions());
    }

    [Fact]
    public void NeighbourTopicFor_AllBands_UsesWildcard()
    {
        PskFeedClient.NeighbourTopicFor("+", "FT8", 291).Should().Be("pskr/filter/v2/+/FT8/+/+/+/+/291/#");
    }

    [Fact]
    public void NeighbourTopicFor_BandModeDxcc_FiltersOnSenderEntity()
    {
        PskFeedClient.NeighbourTopicFor("20M", "ft8", 291).Should().Be("pskr/filter/v2/20m/FT8/+/+/+/+/291/#");
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
