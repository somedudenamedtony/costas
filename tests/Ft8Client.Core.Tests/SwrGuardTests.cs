// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Transmit;

namespace Ft8Client.Core.Tests;

public class SwrGuardTests
{
    private static TxMeterReading R(double? swr, double? watts = 40) => new(0.1, swr, watts);

    [Fact]
    public void Add_TwoHighReadingsInARow_TripsOnce()
    {
        var g = new SwrGuard();
        g.Add(R(3.4)).Should().BeFalse();
        g.Add(R(3.6)).Should().BeTrue();
        g.TrippedAt.Should().Be(3.6);
        g.Add(R(4.0)).Should().BeFalse();
    }

    [Fact]
    public void Add_OneHighReading_DoesNotTrip()
    {
        var g = new SwrGuard();
        g.Add(R(3.4)).Should().BeFalse();
        g.Add(R(1.3)).Should().BeFalse();
        g.Add(R(3.4)).Should().BeFalse();
        g.TrippedAt.Should().BeNull();
    }

    [Fact]
    public void Add_BelowLimit_NeverTrips()
    {
        var g = new SwrGuard();
        for (var i = 0; i < 20; i++) g.Add(R(2.9)).Should().BeFalse();
    }

    [Fact]
    public void Add_UnbelievableReadings_AreIgnored()
    {
        var g = new SwrGuard();
        g.Add(R(46, 46)).Should().BeFalse();   // power read into the SWR slot
        g.Add(R(46, null)).Should().BeFalse(); // above the believable range
        g.Add(R(0)).Should().BeFalse();        // no forward power
        g.Add(R(null)).Should().BeFalse();
        g.TrippedAt.Should().BeNull();
    }

    [Fact]
    public void Add_IgnoredReadingBetweenHighOnes_StillTrips()
    {
        var g = new SwrGuard();
        g.Add(R(3.2)).Should().BeFalse();
        g.Add(R(null)).Should().BeFalse();
        g.Add(R(3.3)).Should().BeTrue();
    }

    [Fact]
    public void Add_CustomLimit_TripsAtThatLimit()
    {
        var g = new SwrGuard(2.0);
        g.Add(R(2.1)).Should().BeFalse();
        g.Add(R(2.2)).Should().BeTrue();
    }

    [Fact]
    public void StopText_FormatsOneDecimal_InvariantCulture()
    {
        SwrGuard.StopText(3.456).Should().Be("Transmit stopped: SWR 3.5. Check the antenna and tuner before sending again.");
    }
}
