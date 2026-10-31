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
using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Audio.Backends;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Data.Settings;
using Ft8Client.Integrations.ReverseBeacon;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public sealed class ReverseBeaconServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 31, 4, 40, 30, DateTimeKind.Utc);
    private readonly string _dir = Directory.CreateTempSubdirectory("costas-rbnservice-").FullName;
    private readonly TcpListener _relay = new(IPAddress.Loopback, 0);

    private sealed class NoAudio : ISlotSource
    {
        public void BeginSlot(DateTime slotStartUtc, Mode mode)
        {
        }

        public void CancelSlot()
        {
        }

        public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode) => null;
    }

    private sealed class NoDecoder : IDecoder
    {
        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct) => throw new NotSupportedException();
    }

    public void Dispose()
    {
        _relay.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private (ReverseBeaconService Service, Session Session, SpotRepository Spots, AppSettingsAccessor Settings) Create(bool enabled)
    {
        var clock = new ManualClock(Now);
        var freqs = ReferenceData.LoadFrequencies();
        var countries = ReferenceData.LoadCountries();
        var tx = new Transmitter(new SimulatedRig(clock), new NullAudioOutput { Now = () => clock.UtcNow }, clock, freqs);
        var session = new Session(new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Ft8 }, new NoDecoder(),
            new NoAudio(), tx, clock, countries, freqs);
        var s = new AppSettings();
        s.Profile.Callsign = "W7LIT";
        s.Profile.Grid = "DN40";
        s.ReverseBeacon.Enabled = enabled;
        var settings = new AppSettingsAccessor(new SettingsStore(Path.Combine(_dir, "settings.json")), s);
        var db = new SqliteDatabase(Path.Combine(_dir, "log.db"));
        var spots = new SpotRepository(db);
        _relay.Start();
        var relayPort = ((IPEndPoint)_relay.LocalEndpoint).Port;
        // The FT8 relay is the local one; the CW/RTTY relay is an address nobody listens on.
        var service = new ReverseBeaconService(session, settings, spots, new CallsignCacheRepository(db), countries, clock,
            port => new RbnFeedClient("127.0.0.1", port == RbnFeedClient.DigitalPort ? relayPort : 1, () => clock.UtcNow));
        return (service, session, spots, settings);
    }

    private Task ServeAsync(string lines) => Task.Run(async () =>
    {
        using var c = await _relay.AcceptTcpClientAsync();
        var s = c.GetStream();
        await s.WriteAsync(Encoding.ASCII.GetBytes("Please enter your call: "));
        await s.ReadAtLeastAsync(new byte[64], 1);
        await s.WriteAsync(Encoding.ASCII.GetBytes(lines));
        await Task.Delay(TimeSpan.FromSeconds(10));
    });

    private static async Task Until(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(50);
    }

    [Fact]
    public async Task Start_SkimmerSpotsMe_AddsAReportAndStoresIt()
    {
        var (service, session, spots, _) = Create(enabled: true);
        await using var owned = service;
        // Already heard by PSK Reporter with a locator: the RBN spot uses it.
        spots.Add(new SpotRecord { MyCall = "W7LIT", RxCall = "KM3T", RxGrid = "FN42", Band = "40m", Mode = "FT8", Snr = -5, TimeUtc = Now.AddMinutes(-30) });
        _ = ServeAsync(
            "DX de KM3T-2-#:  14074.0  W7LIT          FT8    -8 dB  CQ      0440Z\r\n" +
            "DX de KM3T-#:    14025.0  W7LIT          CW     20 dB  22 WPM  CQ      0440Z\r\n");
        service.Start();

        await Until(() => session.Snapshot.Reports.Count > 0);

        var r = session.Snapshot.Reports.Should().ContainSingle("the CW spot is not of a mode Costas sends").Subject;
        r.ReceiverCall.Should().Be("KM3T-2");
        r.ReceiverGrid.Should().Be("FN42");
        r.Band.Should().Be("20m");
        r.Snr.Should().Be(-8);
        spots.Since(Now.AddHours(-1), "20m").Should().ContainSingle().Which.Source.Should().Be(ReverseBeaconService.Source);
        var status = session.Snapshot.Services[ServiceNames.Rbn];
        status.Health.Should().Be(ServiceHealth.Ok, "spots arrive while either relay answers");
        status.Message.Should().StartWith("FT8/FT4 spots connected; RTTY spots: ");
    }

    [Fact]
    public async Task SettingsChanged_TurnedOff_StopsFollowing()
    {
        var (service, session, _, settings) = Create(enabled: true);
        await using var _ = service;
        service.Start();
        service.FollowedCall.Should().Be("W7LIT");

        settings.Update(s => s.ReverseBeacon.Enabled = false);
        await service.Settled;

        service.FollowedCall.Should().BeEmpty();
        session.Snapshot.Services[ServiceNames.Rbn].Health.Should().Be(ServiceHealth.Off);
    }

    [Theory]
    [InlineData("W3LPL-2", "W3LPL")]
    [InlineData("W3LPL", "W3LPL")]
    [InlineData("VE2/W3LPL-12", "VE2/W3LPL")]
    public void LookupCall_SecondSkimmer_UsesTheStation(string skimmer, string expected) =>
        ReverseBeaconService.LookupCall(skimmer).Should().Be(expected);
}
