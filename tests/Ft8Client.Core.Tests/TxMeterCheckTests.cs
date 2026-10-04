// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Transmit;

namespace Ft8Client.Core.Tests;

public class TxMeterCheckTests
{
    private static TxMeterResult Eval(params (double? Alc, double? Swr)[] r) =>
        TxMeterCheck.Evaluate(r.Select(x => new TxMeterReading(x.Alc, x.Swr)));

    [Fact]
    public void Evaluate_NoReadings_NoValuesAndNoWarning()
    {
        Eval().Should().Be(TxMeterResult.None);
        Eval((null, null), (null, null)).Should().Be(TxMeterResult.None);
    }

    [Fact]
    public void Evaluate_LowAlcAndGoodSwr_NoWarning()
    {
        var r = Eval((0.1, 1.2), (0.2, 1.3), (0.1, 1.3));
        r.Level.Should().Be(TxMeterLevel.Ok);
        r.Warning.Should().BeNull();
        r.Alc.Should().BeApproximately(0.1, 1e-9);
        r.Swr.Should().BeApproximately(1.3, 1e-9);
    }

    [Fact]
    public void Evaluate_SwrTwoPointThree_CautionToCheckAntenna()
    {
        var r = Eval((0.0, 2.3), (0.0, 2.3));
        r.Level.Should().Be(TxMeterLevel.Caution);
        r.Warning.Should().Be("SWR 2.3 on the last transmission. Check the antenna or tuner.");
    }

    [Fact]
    public void Evaluate_SwrThreeOrMore_Critical()
    {
        var r = Eval((0.0, 3.4), (0.0, 3.6), (0.0, 3.5));
        r.Level.Should().Be(TxMeterLevel.Critical);
        r.Warning.Should().StartWith("SWR 3.5 on the last transmission. Check the antenna and tuner before sending again.");
    }

    [Fact]
    public void Evaluate_AlcHigh_CautionToLowerAudio()
    {
        var r = Eval((0.7, 1.1), (0.7, 1.1));
        r.Level.Should().Be(TxMeterLevel.Caution);
        r.Warning.Should().StartWith("ALC 70% on the last transmission").And.Contain("Lower Transmit level");
    }

    [Fact]
    public void Evaluate_HighSwrAndAlc_BothReportedAndMostSevereWins()
    {
        var r = Eval((0.9, 3.2));
        r.Level.Should().Be(TxMeterLevel.Critical);
        r.Warning.Should().Contain("SWR 3.2").And.Contain("ALC 90%");
    }

    [Fact]
    public void Evaluate_OneSpikeAmongGoodReadings_IgnoredByMedian()
    {
        Eval((1.0, 4.0), (0.1, 1.2), (0.1, 1.2)).Level.Should().Be(TxMeterLevel.Ok);
    }

    [Fact]
    public void Evaluate_SwrBelowOne_TreatedAsNotMeasured()
    {
        // Radios report SWR 0 when they measured no forward power.
        var r = Eval((0.2, 0.0), (0.2, 0.0));
        r.Swr.Should().BeNull();
        r.Alc.Should().BeApproximately(0.2, 1e-9);
        r.Level.Should().Be(TxMeterLevel.Ok);
    }
}
