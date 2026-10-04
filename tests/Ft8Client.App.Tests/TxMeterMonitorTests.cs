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
}
