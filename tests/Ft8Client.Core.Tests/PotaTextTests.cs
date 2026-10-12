// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Pota;

namespace Ft8Client.Core.Tests;

public class PotaTextTests
{
    [Fact]
    public void Why_NewPark_NamesReference()
    {
        PotaText.Why(new ParkInfo("US-1234", null, null, true)).Should().Be("New park US-1234");
    }

    [Fact]
    public void Why_WorkedPark_ParkAndReference()
    {
        PotaText.Why(new ParkInfo("US-1234", null, null, false)).Should().Be("Park US-1234");
    }

    [Fact]
    public void Why_NoSpot_Pota()
    {
        PotaText.Why(new ParkInfo(null, null, null, false)).Should().Be("POTA");
    }

    [Fact]
    public void Why_Null_Empty()
    {
        PotaText.Why(null).Should().BeEmpty();
        PotaText.Details(null).Should().BeEmpty();
    }

    [Fact]
    public void Details_WithNameAndLocation_JoinsThem()
    {
        PotaText.Details(new ParkInfo("US-1234", "Test Lake State Park", "US-ZZ", true))
            .Should().Be("New park US-1234 · Test Lake State Park, US-ZZ");
    }

    [Fact]
    public void Details_NoName_WhyOnly()
    {
        PotaText.Details(new ParkInfo("US-1234", null, " ", false)).Should().Be("Park US-1234");
    }
}
