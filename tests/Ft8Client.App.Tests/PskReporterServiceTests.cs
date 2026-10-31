// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Audio.Backends;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Data.Settings;
using Ft8Client.Integrations.PskReporter;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public sealed class PskReporterServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("costas-pskservice-").FullName;

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
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private (PskReporterService Service, AppSettingsAccessor Settings) Create(string callsign)
    {
        var clock = new ManualClock(new DateTime(2026, 1, 4, 2, 53, 59, DateTimeKind.Utc));
        var freqs = ReferenceData.LoadFrequencies();
        var tx = new Transmitter(new SimulatedRig(clock), new NullAudioOutput { Now = () => clock.UtcNow }, clock, freqs);
        var session = new Session(new SessionConfig { MyCall = callsign, MyGrid = "DN40", Band = "20m", Mode = Mode.Ft8 }, new NoDecoder(),
            new NoAudio(), tx, clock, ReferenceData.LoadCountries(), freqs);
        var s = new AppSettings();
        s.Profile.Callsign = callsign;
        s.Profile.Grid = "DN40";
        s.PskReporter.FeedEnabled = true;
        var settings = new AppSettingsAccessor(new SettingsStore(Path.Combine(_dir, "settings.json")), s);
        var spots = new SpotRepository(new SqliteDatabase(Path.Combine(_dir, "log.db")));
        // A broker nobody listens on: the feed keeps trying without reaching the network.
        var service = new PskReporterService(session, settings, spots, ReferenceData.LoadCountries(), freqs, clock, uploadsAllowed: false,
            () => new PskFeedClient(null, "127.0.0.1", 1, tls: false));
        return (service, settings);
    }

    [Fact]
    public async Task Start_CallsignSetLater_StartsTheFeedForIt()
    {
        var (service, settings) = Create(string.Empty);
        await using var _ = service;
        service.Start();
        service.FeedRunning.Should().BeFalse("no callsign yet (first run, before Setup)");

        settings.Update(s => s.Profile.Callsign = "W7LIT");
        await service.Settled;

        service.FeedRunning.Should().BeTrue();
        service.FollowedCall.Should().Be("W7LIT");
    }

    [Fact]
    public async Task SettingsChanged_ProfileCallsignChanges_FollowsTheNewCall()
    {
        var (service, settings) = Create("W7LIT");
        await using var _ = service;
        service.Start();
        service.FollowedCall.Should().Be("W7LIT");

        settings.Update(s => s.Profile.Callsign = "K7ABC");
        await service.Settled;
        service.FollowedCall.Should().Be("K7ABC");
        service.FeedRunning.Should().BeTrue();

        settings.Update(s => s.PskReporter.FeedEnabled = false);
        await service.Settled;
        service.FeedRunning.Should().BeFalse();
    }
}
