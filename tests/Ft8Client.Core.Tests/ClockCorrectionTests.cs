// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class ClockCorrectionTests
{
    private static readonly TimeSpan Rtt = TimeSpan.FromMilliseconds(60);

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Next_ClockOffAndGoodMeasurement_CorrectsByTheOffset()
    {
        ClockCorrection.Next(true, S(1.2346), Rtt, TimeSpan.Zero).Should().Be(TimeSpan.FromMilliseconds(1235));
        ClockCorrection.Next(true, S(-2.5), Rtt, TimeSpan.Zero).Should().Be(S(-2.5));
    }

    [Fact]
    public void Next_SettingOff_RemovesTheCorrection()
    {
        ClockCorrection.Next(false, S(1.5), Rtt, S(1.5)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Next_NoServerOrSlowRoundTrip_KeepsTheCurrentCorrection()
    {
        ClockCorrection.Next(true, null, TimeSpan.Zero, S(0.7)).Should().Be(S(0.7));
        ClockCorrection.Next(true, S(3), TimeSpan.FromMilliseconds(800), S(0.7)).Should().Be(S(0.7));
    }

    [Fact]
    public void Next_SmallChange_KeepsTheCurrentCorrection()
    {
        ClockCorrection.Next(true, S(0.05), Rtt, TimeSpan.Zero).Should().Be(TimeSpan.Zero);
        ClockCorrection.Next(true, S(0.75), Rtt, S(0.7)).Should().Be(S(0.7));
        ClockCorrection.Next(true, S(0.85), Rtt, S(0.7)).Should().Be(S(0.85));
    }

    [Fact]
    public void Next_OffsetOverAMinute_IsNotCorrected()
    {
        ClockCorrection.Next(true, S(3600), Rtt, TimeSpan.Zero).Should().Be(TimeSpan.Zero);
        ClockCorrection.Next(true, S(-61), Rtt, S(1)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void CorrectedClock_Correction_ShiftsWallTimeOnly()
    {
        var inner = new ManualClock(new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc));
        var c = new CorrectedClock(inner);
        c.UtcNow.Should().Be(inner.UtcNow);
        c.SetCorrection(S(-1.5));
        c.UtcNow.Should().Be(inner.UtcNow - S(1.5));
        c.Ticks.Should().Be(inner.Ticks);
        c.Correction.Should().Be(S(-1.5));
    }

    [Fact]
    public void Text_Correction_SaysBy()
    {
        ClockCorrection.Text(TimeSpan.Zero).Should().Be("Not corrected");
        ClockCorrection.Text(S(1.24)).Should().Be("Corrected by +1.24 s");
    }
}
