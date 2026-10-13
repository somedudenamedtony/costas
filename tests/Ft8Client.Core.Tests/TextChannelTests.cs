// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Keyboard;

namespace Ft8Client.Core.Tests;

public class TextChannelTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Add_TextBeforeAnyCall_ReturnsNull()
    {
        new TextChannel().Add("CQ CQ CQ ", T0, 1000, -5).Should().BeNull();
    }

    [Fact]
    public void Add_CallArrivesInPieces_FoundOnceComplete()
    {
        var ch = new TextChannel();
        ch.Add("CQ CQ DE KP4", T0, 1000, -5).Should().BeNull();
        ch.Add("J", T0.AddSeconds(1), 1000, -5).Should().BeNull();
        var h = ch.Add("RS KP4JRS K\n", T0.AddSeconds(2), 1001, -4);
        h!.Call.Should().Be("KP4JRS");
        h.Cq.Should().BeTrue();
        h.OverEnded.Should().BeTrue();
        h.OffsetHz.Should().Be(1001);
    }

    [Fact]
    public void Add_LaterTextWithoutCall_KeepsStation()
    {
        var ch = new TextChannel();
        ch.Add("W7LIT DE EA8TL KN ", T0, 1000, -5);
        var h = ch.Add("GE TONY TNX FER CALL ", T0.AddSeconds(5), 1000, -5);
        h!.Call.Should().Be("EA8TL");
        h.To.Should().Be("W7LIT");
        h.Cq.Should().BeFalse();
    }

    [Fact]
    public void Add_DifferentSender_TakesOver()
    {
        var ch = new TextChannel();
        ch.Add("W7LIT DE EA8TL KN ", T0, 1000, -5);
        ch.Add("CQ CQ DE JA7QVI JA7QVI K ", T0.AddSeconds(10), 1000, -5)!.Call.Should().Be("JA7QVI");
        ch.Call.Should().Be("JA7QVI");
    }

    [Fact]
    public void Add_AfterQuietMinute_ForgetsStation()
    {
        var ch = new TextChannel();
        ch.Add("W7LIT DE EA8TL KN ", T0, 1000, -5);
        ch.Add("GE TONY ", T0.AddSeconds(61), 1000, -5).Should().BeNull();
    }

    [Fact]
    public void RecentText_LongText_KeepsLastSixtyCharactersOnOneLine()
    {
        var ch = new TextChannel();
        ch.Add(new string('A', 50) + "\n" + new string('B', 20), T0, 1000, -5);
        ch.RecentText.Should().HaveLength(TextChannel.RecentLength).And.EndWith(new string('B', 20)).And.NotContain("\n");
    }
}
