// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Tests;

public class ListStabilityTests
{
    private sealed class Clock
    {
        public DateTime Now = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void Offer_PointerOverList_HoldsUntilPointerLeaves()
    {
        var c = new Clock();
        var s = new LineStabilizer<string>(() => c.Now);
        s.Offer("A").Should().BeTrue();
        s.PointerEntered();
        s.Offer("B").Should().BeFalse("no re-rank under the pointer");
        s.Applied.Should().Be("A");
        s.HasPending.Should().BeTrue("the List updated pill shows");
        s.Offer("C").Should().BeFalse();
        s.PointerExited();
        s.Applied.Should().Be("C");
        s.HasPending.Should().BeFalse();
    }

    [Fact]
    public void Offer_Within300msOfClick_HoldsThenTickApplies()
    {
        var c = new Clock();
        var s = new LineStabilizer<string>(() => c.Now);
        s.Offer("A");
        s.Clicked();
        c.Now = c.Now.AddMilliseconds(299);
        s.Offer("B").Should().BeFalse();
        s.Tick();
        s.Applied.Should().Be("A");
        c.Now = c.Now.AddMilliseconds(2);
        s.Tick();
        s.Applied.Should().Be("B");
    }

    [Fact]
    public void ApplyPending_PillClicked_AppliesEvenUnderPointer()
    {
        var c = new Clock();
        var s = new LineStabilizer<string>(() => c.Now);
        s.Offer("A");
        s.PointerEntered();
        s.Offer("B");
        s.ApplyPending();
        s.Applied.Should().Be("B");
    }

    [Fact]
    public void Offer_NoPointerNoClick_AppliesImmediately()
    {
        var c = new Clock();
        var s = new LineStabilizer<string>(() => c.Now);
        var applied = new List<string>();
        s.AppliedChanged += applied.Add;
        s.Offer("A");
        s.Offer("B");
        applied.Should().Equal("A", "B");
    }
}
