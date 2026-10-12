// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Pileups;

namespace Ft8Client.Core.Tests;

public class PileupTextTests
{
    private static readonly DateTime Slot = new(2026, 1, 4, 20, 29, 30, DateTimeKind.Utc);

    private static Pileup P(params PileupCaller[] callers) => new()
    {
        Target = "JA6VQA",
        Callers = callers,
        CallersSlotUtc = callers.Length == 0 ? null : Slot,
    };

    private static PileupCaller C(string call, int snr, int offset) => new(call, Slot, snr, offset);

    private static PileupPick Pick(bool strongest, bool heard = true, int callers = 3) => new("W7XQ", Slot, callers, heard, strongest);

    [Fact]
    public void Headline_SeveralCallers_CountsThem()
    {
        PileupText.Headline(P(C("W7XQ", -6, 1330), C("K6ODE", -9, 1560), C("N0QRT", -12, 1610)))
            .Should().Be("3 stations called JA6VQA at 20:29:30.");
    }

    [Fact]
    public void Headline_OneCaller_NamesIt()
    {
        PileupText.Headline(P(C("W7XQ", -6, 1330))).Should().Be("W7XQ called JA6VQA at 20:29:30.");
    }

    [Fact]
    public void Headline_NoCallers_Blank()
    {
        PileupText.Headline(P()).Should().BeEmpty();
    }

    [Fact]
    public void Picks_FourOfFiveStrongest_SaysSo()
    {
        var p = P() with { Picks = [Pick(true), Pick(true), Pick(false), Pick(true), Pick(true), Pick(true)] };

        PileupText.Picks(p).Should().Be("Answered the strongest caller you heard in 4 of its last 5 contacts.");
    }

    [Fact]
    public void Picks_UnjudgedSkipped_BlankBelowTwo()
    {
        var p = P() with { Picks = [Pick(true), Pick(true, heard: false), Pick(true, callers: 1)] };

        PileupText.Picks(p).Should().BeEmpty();
    }

    [Fact]
    public void Offset_CallerOverlapsMine_NamesNearest()
    {
        PileupText.Offset(P(C("W7XQ", -6, 1330), C("K6ODE", -9, 1620)), 1650, Mode.Ft8)
            .Should().Be("K6ODE is calling 30 Hz from your offset.");
    }

    [Fact]
    public void Offset_AllClear_SaysClear()
    {
        PileupText.Offset(P(C("W7XQ", -6, 1330)), 1650, Mode.Ft8).Should().Be("Your offset, 1650 Hz, is clear of them.");
    }

    [Fact]
    public void Offset_NoCallers_Blank()
    {
        PileupText.Offset(P(), 1650, Mode.Ft8).Should().BeEmpty();
    }
}
