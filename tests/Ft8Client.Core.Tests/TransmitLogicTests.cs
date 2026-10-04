// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Transmit;

namespace Ft8Client.Core.Tests;

public class TransmitLogicTests
{
    [Fact]
    public void Choose_EmptyBand_KeepsCurrent()
    {
        TxOffsetPicker.Choose([], Mode.Ft8, 1500).Should().Be(new TxOffsetChoice(1500, true));
    }

    [Fact]
    public void Choose_CurrentBusy_MovesToWidestGapInPreferredRange()
    {
        IReadOnlyList<int> slot = [1000, 1080, 1160, 1500, 1520, 1900, 1950];
        var c = TxOffsetPicker.Choose([slot], Mode.Ft8, 1500);
        c.Clear.Should().BeTrue();
        c.OffsetHz.Should().BeInRange(1570, 1850);
        (c.OffsetHz + 50).Should().BeLessThan(1900);
    }

    [Fact]
    public void Choose_NoGapAnywhere_ReportsBusy()
    {
        var all = Enumerable.Range(0, 60).Select(i => 200 + i * 45).ToList();
        var c = TxOffsetPicker.Choose([all], Mode.Ft8, 1500);
        c.Clear.Should().BeFalse();
        c.OffsetHz.Should().Be(1500);
        TxOffsetPicker.Describe(c).Should().Be("Tx offset 1500 Hz · busy");
    }

    [Fact]
    public void Choose_OldSlotsWeighLess()
    {
        // A signal seen only in the oldest slot (weight 0.25) still marks the band busy at the threshold.
        IReadOnlyList<int> none = [];
        IReadOnlyList<int> old = [1490];
        TxOffsetPicker.Choose([none, none, none, old], Mode.Ft8, 1500).OffsetHz.Should().NotBe(1500);
    }

    [Theory]
    [InlineData(new[] { 0.1, 0.2, 0.3 }, 0.2)]
    [InlineData(new[] { 1.1, 0.9, 1.3, 1.0 }, 1.05)]
    public void MedianDt_Values(double[] dts, double expected)
    {
        ClockEstimate.MedianDt(dts).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void TransmitAllowed_ThreeSecondsOff_Blocked()
    {
        ClockEstimate.TransmitAllowed(3.0, 2.0, out var why).Should().BeFalse();
        why.Should().Contain("3.0 s");
        ClockEstimate.TransmitAllowed(-1.5, 2.0, out _).Should().BeTrue();
        ClockEstimate.TransmitAllowed(null, 2.0, out _).Should().BeTrue();
    }
}
