// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Core.Tests;

public class StationTextWhyTests
{
    [Theory]
    [InlineData(NeedTag.NewGrid, "do62xx", "New grid DO62")]
    [InlineData(NeedTag.NewGrid, null, "New grid")]
    [InlineData(NeedTag.NewCountry, "DO62", "New country")]
    [InlineData(NeedTag.NewCall, "DO62", "New call")]
    public void Why_Tag_NamesTheGridForANewGrid(NeedTag tag, string? grid, string expected)
    {
        StationText.Why(tag, grid).Should().Be(expected);
    }

    [Fact]
    public void WhyTip_NewBand_ListsBandsWorked()
    {
        StationText.WhyTip(NeedTag.NewBand, "Canada", ["40m", "15m"], "20m").Should().Be("Canada is in your log on 40 m and 15 m, not on 20 m yet");
        StationText.WhyTip(NeedTag.NewBand, "Japan", ["80m", "40m", "15m"], "20m").Should().Be("Japan is in your log on 80 m, 40 m and 15 m, not on 20 m yet");
        StationText.WhyTip(NeedTag.NewBand, "Japan", ["40m"], "20m").Should().Be("Japan is in your log on 40 m, not on 20 m yet");
    }

    [Fact]
    public void WhyTip_OtherTagsOrNoBands_Empty()
    {
        StationText.WhyTip(NeedTag.NewGrid, "Canada", ["40m"], "20m").Should().BeEmpty();
        StationText.WhyTip(NeedTag.NewBand, "Canada", [], "20m").Should().BeEmpty();
    }

    [Theory]
    [InlineData(HearsYouKind.Direct, -7, null, "hears you at -07")]
    [InlineData(HearsYouKind.Regional, 5, "DO field", "heard in DO field at +05")]
    [InlineData(HearsYouKind.None, null, null, "no reports yet")]
    [InlineData(HearsYouKind.Unavailable, null, null, "")]
    public void ChanceReason_Kind_SaysWhatTheChanceIsBasedOn(HearsYouKind kind, int? snr, string? region, string expected)
    {
        StationText.ChanceReason(new HearsYou(kind, snr, null, region)).Should().Be(expected);
    }
}
