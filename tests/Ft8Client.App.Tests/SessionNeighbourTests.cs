// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Audio.Backends;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class SessionNeighbourTests
{
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

    private sealed class Driver
    {
        public Driver()
        {
            var freqs = ReferenceData.LoadFrequencies();
            var tx = new Transmitter(new SimulatedRig(Clock), new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, freqs);
            Session = new Session(new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Ft8 }, new NoDecoder(),
                new NoAudio(), tx, Clock, ReferenceData.LoadCountries(), freqs);
        }

        public ManualClock Clock { get; } = new(new DateTime(2026, 1, 4, 2, 53, 59, DateTimeKind.Utc));

        public Session Session { get; }
    }

    [Fact]
    public void AddNeighbourReport_NearbyStationsSharedReceivers_ComparedInSnapshot()
    {
        var d = new Driver();
        var t = d.Clock.UtcNow.AddMinutes(-5);
        foreach (var (rx, snr) in new[] { ("DL1ABC", -19), ("G4XYZ", -20), ("F5ABC", -18) })
        {
            d.Session.AddReport(new ReceptionReport(rx, null, null, null, "20m", "FT8", snr, t));
            d.Session.AddNeighbourReport(new NeighbourReport("K7AAA", "DN41", rx, "20m", "FT8", snr + 7, t));
        }
        // Far away, another band, and my own call: all dropped.
        d.Session.AddNeighbourReport(new NeighbourReport("K1ABC", "FN42", "DL1ABC", "20m", "FT8", 0, t));
        d.Session.AddNeighbourReport(new NeighbourReport("K7BBB", "DN41", "DL1ABC", "40m", "FT8", 0, t));
        d.Session.AddNeighbourReport(new NeighbourReport("W7LIT", "DN40", "DL1ABC", "20m", "FT8", 0, t));
        d.Session.Publish();

        var n = d.Session.Snapshot.Neighbours;
        n.Neighbours.Should().Be(1);
        n.Rows.Should().ContainSingle().Which.DiffDb.Should().Be(-7);
    }

    [Fact]
    public void AddNeighbourReport_OtherBands_ShownAsBandOpenings()
    {
        var d = new Driver();
        var t = d.Clock.UtcNow.AddMinutes(-5);
        d.Session.AddNeighbourReport(new NeighbourReport("K7AAA", "DN41", "DL1ABC", "20m", "FT8", -12, t));
        d.Session.AddNeighbourReport(new NeighbourReport("K7BBB", "DN41", "PY2ABC", "15m", "FT8", -15, t));
        d.Session.AddNeighbourReport(new NeighbourReport("K7BBB", "DN41", "LU1ABC", "15m", "FT8", -15, t));
        d.Session.Publish();

        var o = d.Session.Snapshot.BandOpenings;
        o.Keys.Should().BeEquivalentTo("20m", "15m");
        o["15m"].SpotsByContinent.Should().Contain("SA", 2);
    }

    [Fact]
    public void SetBand_SameMode_KeepsReportsForTheBandMenu()
    {
        var d = new Driver();
        var t = d.Clock.UtcNow.AddMinutes(-5);
        d.Session.AddNeighbourReport(new NeighbourReport("K7AAA", "DN41", "DL1ABC", "20m", "FT8", -12, t));
        d.Session.SetBand("40m", Mode.Ft8);
        d.Session.Publish();

        d.Session.Snapshot.BandOpenings.Should().ContainKey("20m");
        d.Session.Snapshot.Neighbours.Neighbours.Should().Be(0);
    }

    [Fact]
    public void SetBand_OtherMode_ClearsNeighbourReports()
    {
        var d = new Driver();
        var t = d.Clock.UtcNow.AddMinutes(-5);
        d.Session.AddNeighbourReport(new NeighbourReport("K7AAA", "DN41", "DL1ABC", "20m", "FT8", -12, t));
        d.Session.SetBand("20m", Mode.Ft4);
        d.Session.Publish();

        d.Session.Snapshot.BandOpenings.Should().BeEmpty();
    }
}
