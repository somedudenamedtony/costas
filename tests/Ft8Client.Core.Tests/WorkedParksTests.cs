// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Pota;

namespace Ft8Client.Core.Tests;

public class WorkedParksTests
{
    [Fact]
    public void Build_SigPota_AddsSigInfo()
    {
        WorkedParks.Build([("pota", " us-1234 ", null)]).Contains("US-1234").Should().BeTrue();
    }

    [Fact]
    public void Build_OtherSig_Ignored()
    {
        WorkedParks.Build([("WWFF", "KFF-1234", null)]).Count.Should().Be(0);
    }

    [Fact]
    public void Build_PotaRefList_AddsEachWithoutLocation()
    {
        var w = WorkedParks.Build([(null, null, "US-1111@US-ZZ,US-2222")]);

        w.Contains("US-1111").Should().BeTrue();
        w.Contains("US-2222").Should().BeTrue();
        w.Count.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("US-")]
    [InlineData("US 1234")]
    public void Normalize_NotAReference_Null(string text)
    {
        WorkedParks.Normalize(text).Should().BeNull();
    }

    [Fact]
    public void With_NewPark_ReturnsCopyAndLeavesOriginal()
    {
        var a = WorkedParks.Empty;
        var b = a.With("us-1234");

        b.Contains("US-1234").Should().BeTrue();
        a.Contains("US-1234").Should().BeFalse();
    }
}
