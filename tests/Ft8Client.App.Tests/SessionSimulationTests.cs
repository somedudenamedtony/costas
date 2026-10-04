// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Stations;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Decoding;

namespace Ft8Client.App.Tests;

public class SessionSimulationTests
{
    private static SessionConfig Config() => new() { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Ft8 };

    private sealed class SilentSource : ISlotSource
    {
        private bool _active;

        public Func<DateTime, Mode, float[]?>? Mixer { get; set; }

        public int Cancelled { get; private set; }

        public void BeginSlot(DateTime slotStartUtc, Mode mode) => _active = true;

        public void CancelSlot()
        {
            _active = false;
            Cancelled++;
        }

        public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode)
        {
            if (!_active) return null;
            _active = false;
            var s = new short[ModeInfo.DecoderSamples(mode)];
            var rng = new Random(slotStartUtc.Second);
            var mix = Mixer?.Invoke(slotStartUtc, mode);
            for (var i = 0; i < s.Length; i++)
                s[i] = (short)Math.Clamp(((rng.NextDouble() - 0.5) * 0.01 + (mix is null ? 0 : mix[i])) * 32767, short.MinValue, short.MaxValue);
            return new SlotAudio(slotStartUtc, mode, s);
        }
    }

    [Fact]
    public async Task Simulate_SampleFolder_ProducesExpectedStationsAndRanking()
    {
        Assert.SkipWhen(Jt9Locator.Find() is null, "jt9 is not installed.");
        var folder = Directory.CreateTempSubdirectory("ft8simfolder").FullName;
        File.Copy(Path.Combine(TestPaths.Samples, "ft8", "210703_133430.wav"), Path.Combine(folder, "a.wav"));
        File.Copy(Path.Combine(TestPaths.Samples, "ft8", "181201_180245.wav"), Path.Combine(folder, "b.wav"));
        var d = new SimDriver(new SimulatedSlotSource(folder), Config());

        await d.RunSlotsAsync(3);

        var snap = d.Session.Snapshot;
        var calls = snap.Stations.Select(s => s.Call).ToList();
        // From the golden decodes of the two sample files.
        calls.Should().Contain(["F5RXL", "DL8YHR", "EA2BFM", "W7BOB", "NT2A", "K1BAA", "EA6VQ", "F5BZB"]);
        snap.Stations.Single(s => s.Call == "F5RXL").State.Should().Be(StationState.CallingCq);
        snap.Stations.Single(s => s.Call == "DL8YHR").CqModifier.Should().Be("DX");
        snap.Stations.Single(s => s.Call == "EA6VQ").Partner.Should().Be("WM3PEN");
        snap.RawDecodes.Count.Should().BeGreaterThanOrEqualTo(43);
        snap.LogIsEmpty.Should().BeTrue();
        snap.Rank.Line.Select(r => r.Station.Call).Should().Contain("F5RXL", "a CQ with no log is a new call and callable");
        snap.Rank.Line.Select(r => r.Station.Call).Should().Contain("DL8YHR", "CQ DX from Germany is callable from the USA");
        snap.Summary!.StationsHeard.Should().Be(snap.Stations.Count);
        d.Rig.PttLog.Should().BeEmpty("nothing is transmitted without an operator command");
    }

    [Fact]
    public async Task SimulatePartner_TenContactsInARow_LogAndEnqueueWithNoFurtherInput()
    {
        Assert.SkipWhen(Jt9Locator.Find() is null, "jt9 is not installed.");
        var partner = SimulatedPartner.FromFile(Path.Combine(TestPaths.Samples, "partners.txt"));
        var source = new SilentSource { Mixer = partner.Mix };
        var db = new SqliteDatabase(Path.Combine(Directory.CreateTempSubdirectory("ft8partner").FullName, "log.db"));
        var qsos = new QsoRepository(db);
        var queue = new UploadQueueRepository(db);
        var log = new DatabaseSessionLog(qsos, queue, "home", () => true);
        var config = Config() with { Contact = new ContactSettings { WatchdogMinutes = 60 } };
        var d = new SimDriver(source, config, log);
        d.Session.Transmitting += partner.OnTransmitted;

        await d.RunSlotsAsync(4);
        d.Rig.PttLog.Should().BeEmpty("no transmission before the operator's command");

        d.Session.ToggleCq();
        for (var i = 0; i < 80 && qsos.Count() < 10; i++) await d.RunSlotsAsync(1);

        var logged = qsos.List().OrderBy(q => q.QsoDateOn).ToList();
        logged.Select(q => q.Call).Take(10).Should().Equal("K1ABC", "VE7HLB", "JA1QRS", "ZL2RPA", "LU8DCM", "PY2WRA", "EA5HVK", "OH2KQA", "VK3BMT", "KH6TU");
        logged.Should().OnlyContain(q => q.RstSent != null && q.RstRcvd != null && q.Band == "20m" && q.Mode == "FT8" && q.StationCallsign == "W7LIT");
        queue.Count().Should().Be(logged.Count);
        logged.Should().OnlyContain(q => q.UploadState == UploadState.Queued);
        d.Session.Snapshot.Tonight.Should().HaveCount(logged.Count);
        // PTT was only ever asserted for our planned transmissions, each released.
        d.Rig.PttLog.Count(p => p.On).Should().Be(d.Rig.PttLog.Count(p => !p.On));
    }
}
