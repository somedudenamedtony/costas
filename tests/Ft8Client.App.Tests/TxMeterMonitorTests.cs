// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Audio.Backends;
using Ft8Client.Core;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class TxMeterMonitorTests
{
    private sealed class Station
    {
        public readonly ManualClock Clock = new(new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc));
        public readonly SimulatedRig Radio;
        public readonly Transmitter Tx;
        public readonly TxMeterMonitor Monitor;
        public readonly List<TxMeterResult> Measured = [];
        public readonly List<TxMeterReading> Live = [];

        public Station(TxMeterReading meters)
        {
            Radio = new SimulatedRig(Clock) { TxMeters = meters };
            Tx = new Transmitter(Radio, new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, ReferenceData.LoadFrequencies());
            // Short timings so a 0.4 s tone gets several readings.
            Monitor = new TxMeterMonitor(Tx, () => Radio)
            {
                Settle = TimeSpan.FromMilliseconds(20),
                Interval = TimeSpan.FromMilliseconds(20),
                StopBeforeEnd = TimeSpan.FromMilliseconds(100),
            };
            Monitor.Measured += Measured.Add;
            Monitor.Read += r => { lock (Live) Live.Add(r); };
        }

        public Task<string?> Tone() => Tx.TuneAsync(1500, 0.4, new TxGuardInput("20m", 14_074_000, 0.0, 2.0, null, true));
    }

    [Fact]
    public async Task Tone_HighSwrAndAlc_ReportsCriticalWarning()
    {
        var s = new Station(new TxMeterReading(0.8, 3.5));
        (await s.Tone()).Should().BeNull();
        var result = await s.Monitor.Idle;

        result.Should().NotBeNull();
        result!.Level.Should().Be(TxMeterLevel.Critical);
        s.Measured.Should().ContainSingle().Which.Should().Be(result);
        TxMeterText.Warning(result).Should().Be(("SWR 3.5 · ALC 80%", result.Warning!, TextKind.Critical));
    }

    [Fact]
    public async Task Tone_RadioWithoutMeters_ReportsNothing()
    {
        var s = new Station(TxMeterReading.None);
        await s.Tone();
        (await s.Monitor.Idle).Should().BeNull();
        s.Measured.Should().BeEmpty();
        s.Live.Should().BeEmpty();
    }

    [Fact]
    public async Task Tone_PowerMeter_ReportsEachReadingLiveThenTheMedian()
    {
        var s = new Station(new TxMeterReading(0.05, 1.3, 24));
        await s.Tone();
        var result = await s.Monitor.Idle;
        lock (s.Live) s.Live.Should().NotBeEmpty().And.OnlyContain(r => r == new TxMeterReading(0.05, 1.3, 24));
        result!.Watts.Should().Be(24);
    }

    [Fact]
    public void Strip_Live_ShowsTheReadingNotTheLastResult()
    {
        var held = new TxMeterResult(0.8, 3.5, TxMeterLevel.Critical, "SWR 3.5 on the last transmission.", 90);
        var m = TxMeterText.Strip(new TxMeterReading(0.06, 1.3, 24.4), held, 100)!;
        m.Live.Should().BeTrue();
        m.Caption.Should().Be("TX");
        (m.Watts, m.Alc, m.AlcKind, m.Swr, m.SwrKind).Should().Be(("24 W", "OK", TextKind.Normal, "1.3", TextKind.Normal));
        m.WattsBar.Should().BeApproximately(0.244, 1e-9);
        m.SwrBar.Should().BeApproximately(0.15, 1e-9);
        m.Tip.Should().NotContain("SWR 3.5", "a warning about the last transmission does not label live readings");
    }

    [Fact]
    public void Strip_AfterTransmission_HoldsTheResultWithItsWarning()
    {
        var held = new TxMeterResult(0.6, 2.6, TxMeterLevel.Caution, "SWR 2.6 on the last transmission. Check the antenna or tuner.", 24);
        var m = TxMeterText.Strip(null, held, 50)!;
        m.Live.Should().BeFalse();
        m.Caption.Should().Be("Last TX");
        (m.Alc, m.AlcKind, m.Swr, m.SwrKind).Should().Be(("High", TextKind.Caution, "2.6", TextKind.Caution));
        m.WattsBar.Should().BeApproximately(0.48, 1e-9, "the power bar is a share of the radio's most");
        m.Tip.Should().Be(held.Warning);
        TxMeterText.Strip(null, held with { Swr = 3.2 }, null)!.SwrKind.Should().Be(TextKind.Critical);
    }

    [Fact]
    public void Strip_NothingReportedOrImpossibleValues_HidesThoseMeters()
    {
        TxMeterText.Strip(null, null, null).Should().BeNull();
        TxMeterText.Strip(TxMeterReading.None, TxMeterResult.None, null).Should().BeNull();
        // ALC outside 0 to 1 is not an ALC reading (an SWR value read out of step); SWR below 1 means no forward power.
        var m = TxMeterText.Strip(new TxMeterReading(1.7, 0, 5), null, null)!;
        (m.Alc, m.Swr, m.Watts).Should().Be(((string?)null, (string?)null, "5 W"));
    }

    [Fact]
    public async Task Tone_ReadingsWithinLimits_NoWarningAndNeverTouchesPtt()
    {
        var s = new Station(new TxMeterReading(0.1, 1.2));
        await s.Tone();
        var result = await s.Monitor.Idle;
        result!.Warning.Should().BeNull();
        TxMeterText.Warning(result).Text.Should().BeEmpty();
        TxMeterText.Summary(result).Should().Be("ALC 10% · SWR 1.2");
        s.Radio.PttLog.Select(p => p.On).Should().Equal(true);
    }

    [Fact]
    public void Summary_MeterNotReported_SaysSo()
    {
        TxMeterText.Summary(null).Should().Be("—");
        TxMeterText.Summary(new TxMeterResult(0.25, null, TxMeterLevel.Ok, null)).Should().Be("ALC 25% · SWR not reported");
    }

    [Fact]
    public async Task Tone_RadioReportsPower_KeptForTheLog()
    {
        var s = new Station(TxMeterReading.None);
        s.Monitor.PowerWatts.Should().BeNull();
        s.Radio.PowerWatts = 40;
        await s.Tone();
        await s.Monitor.Idle;
        s.Monitor.PowerWatts.Should().Be(40);

        s.Radio.PowerWatts = null;
        await s.Tone();
        await s.Monitor.Idle;
        s.Monitor.PowerWatts.Should().BeNull("a transmission whose power could not be read falls back to Settings");
    }
}
