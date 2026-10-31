// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Ft8Client.Core.Services;
using Ft8Client.Integrations.ReverseBeacon;

namespace Ft8Client.Integrations.Tests;

public class ReverseBeaconTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 4, 40, 30, DateTimeKind.Utc);

    [Fact]
    public void Parse_CwLine_ReadsEveryField()
    {
        var spot = RbnSpotParser.Parse("DX de HB9DCO-#:  14011.9  UP2L           CW    17 dB  35 WPM  CQ      0439Z\r", Now);

        spot.Should().Be(new RbnSpot("HB9DCO", "UP2L", 14_011_900, "CW", 17, new DateTime(2026, 10, 31, 4, 39, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Parse_Ft8LineWithoutSpeed_ReadsNegativeSnr()
    {
        var spot = RbnSpotParser.Parse("DX de KM3T-2-#:   14074.0  W7LIT          FT8   -12 dB  CQ      0440Z", Now);

        spot.Should().NotBeNull();
        spot!.Skimmer.Should().Be("KM3T-2");
        spot.SpottedCall.Should().Be("W7LIT");
        spot.Mode.Should().Be("FT8");
        spot.Snr.Should().Be(-12);
        spot.FrequencyHz.Should().Be(14_074_000);
    }

    [Fact]
    public void Parse_RttyLine_ReadsBaudAsSpeed()
    {
        var spot = RbnSpotParser.Parse("DX de N6TV-#:     7085.2  W7LIT          RTTY  25 dB  45 BPS  CQ      0440Z", Now);

        spot.Should().NotBeNull();
        spot!.Mode.Should().Be("RTTY");
        spot.Snr.Should().Be(25);
    }

    [Fact]
    public void Parse_TimeAheadOfNow_IsYesterday()
    {
        var spot = RbnSpotParser.Parse("DX de N6TV-#:     7085.2  W7LIT          RTTY  25 dB  45 BPS  CQ      2359Z", Now);

        spot!.TimeUtc.Should().Be(new DateTime(2026, 10, 30, 23, 59, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("Please enter your call:")]
    [InlineData("Hello W7LIT, this is RELAY")]
    [InlineData("DX de N6TV-#:     7085.2  W7LIT          RTTY  25 dB  45 BPS  CQ      2599Z")]
    [InlineData("")]
    public void Parse_NotASpot_ReturnsNull(string line) => RbnSpotParser.Parse(line, Now).Should().BeNull();

    [Fact]
    public async Task Start_RelayAnswers_LogsInWithMyCallAndRaisesOnlyMySpots()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var login = new TaskCompletionSource<string>();
        var ct = TestContext.Current.CancellationToken;
        _ = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync(ct);
            var s = c.GetStream();
            // Telnet negotiation first (IAC WILL ECHO), then the prompt without a new line, as the relays send it.
            await s.WriteAsync(new byte[] { 255, 251, 1 }, ct);
            await s.WriteAsync(Encoding.ASCII.GetBytes("Please enter your call: "), ct);
            var buf = new byte[64];
            var n = await s.ReadAsync(buf, ct);
            login.TrySetResult(Encoding.ASCII.GetString(buf, 0, n));
            await s.WriteAsync(Encoding.ASCII.GetBytes(
                "DX de KM3T-#:    14074.0  K1ABC          FT8   -10 dB  CQ      0440Z\r\n" +
                "DX de KM3T-#:    14074.0  W7LIT          FT8    -8 dB  CQ      0440Z\r\n" +
                "DX de N6TV-#:     7085.2  W7LIT/P        RTTY  25 dB  45 BPS  CQ      0440Z\r\n" +
                "DX de N6TV-#:     7085.2  W7LITX         RTTY  25 dB  45 BPS  CQ      0440Z\r\n"), ct);
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }, ct);
        var spots = new List<RbnSpot>();
        var got = new TaskCompletionSource();
        await using var feed = new RbnFeedClient("127.0.0.1", port, () => Now);
        feed.Spot += s =>
        {
            lock (spots) spots.Add(s);
            got.TrySetResult();
        };
        feed.Start("w7lit");

        (await login.Task.WaitAsync(TimeSpan.FromSeconds(10), ct)).Should().Be("W7LIT\r\n");
        await got.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        await Task.Delay(200, ct);

        feed.Connected.Should().BeTrue();
        lock (spots) spots.Should().ContainSingle().Which.Should().Be(new RbnSpot("KM3T", "W7LIT", 14_074_000, "FT8", -8, Now.Date.AddHours(4).AddMinutes(40)));
        feed.LinesRead.Should().Be(4);
    }

    [Fact]
    public async Task Start_RelayUnreachable_ReportsDown()
    {
        var down = new TaskCompletionSource<ServiceStatus>();
        await using var feed = new RbnFeedClient("127.0.0.1", 1, () => Now);
        feed.StatusChanged += st => down.TrySetResult(st);
        feed.Start("W7LIT");

        var st = await down.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        st.Health.Should().Be(ServiceHealth.Down);
        feed.Connected.Should().BeFalse();
    }
}
