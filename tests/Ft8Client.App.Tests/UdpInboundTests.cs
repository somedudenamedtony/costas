// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Messages;
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.App.Tests;

public class UdpInboundTests
{
    private static readonly DateTime Slot = new(2026, 10, 12, 21, 40, 15, DateTimeKind.Utc);

    private static RawDecodeLine Line(DateTime slot, string text, bool toMe = false, bool tx = false) =>
        new(new Decode(slot, -10, 0.1, 1200, text, false), MessageParser.Parse(text), null, toMe, tx);

    private static WsjtxReply Reply(string text, DateTime? slot = null) =>
        new("Costas", (slot ?? Slot).TimeOfDay, -10, 0.1, 1200, "~", text, false, 0);

    [Fact]
    public void Sender_RecentCq_CallsSender()
    {
        UdpReplyRule.Sender(Reply("CQ K1ABC FN42"), [Line(Slot, "CQ K1ABC FN42")]).Should().Be("K1ABC");
    }

    [Fact]
    public void Sender_StationCallingMe_Answers()
    {
        UdpReplyRule.Sender(Reply("W7LIT K1ABC FN42"), [Line(Slot, "W7LIT K1ABC FN42", toMe: true)]).Should().Be("K1ABC");
    }

    [Fact]
    public void Sender_MessageToSomeoneElse_Ignored()
    {
        UdpReplyRule.Sender(Reply("N0CALL K1ABC -10"), [Line(Slot, "N0CALL K1ABC -10")]).Should().BeNull();
    }

    [Fact]
    public void Sender_NoMatchingDecode_Ignored()
    {
        UdpReplyRule.Sender(Reply("CQ K1ABC FN42"), [Line(Slot.AddSeconds(-15), "CQ K1ABC FN42")]).Should().BeNull();
        UdpReplyRule.Sender(Reply("CQ K1ABC FN42"), []).Should().BeNull();
    }

    [Fact]
    public void Sender_MyOwnTransmission_Ignored()
    {
        UdpReplyRule.Sender(Reply("CQ W7LIT DN40"), [Line(Slot, "CQ W7LIT DN40", tx: true)]).Should().BeNull();
    }

    [Fact]
    public void Highlight_Call_ColoursItsLines()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply([Line(Slot, "CQ K1ABC FN42"), Line(Slot, "CQ G4AAA IO91")]);

        vm.Highlight(new WsjtxHighlight("Costas", "k1abc", new WsjtxColor(255, 255, 255, 0), new WsjtxColor(255, 0, 0, 0), false));
        vm.ApplyHighlights();

        vm.Lines.Single(l => l.Message == "CQ K1ABC FN42").Background.Should().Be(new WsjtxColor(255, 255, 255, 0));
        vm.Lines.Single(l => l.Message == "CQ G4AAA IO91").Background.Should().BeNull();
    }

    [Fact]
    public void Highlight_LastOnly_LeavesEarlierSlots()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply([Line(Slot.AddSeconds(-30), "CQ K1ABC FN42"), Line(Slot, "W7LIT K1ABC FN42")]);

        vm.Highlight(new WsjtxHighlight("Costas", "K1ABC", new WsjtxColor(255, 255, 255, 0), new WsjtxColor(255, 0, 0, 0), true));
        vm.ApplyHighlights();

        vm.Lines.Single(l => l.Message == "W7LIT K1ABC FN42").Foreground.Should().NotBeNull();
        vm.Lines.Single(l => l.Message == "CQ K1ABC FN42").Foreground.Should().BeNull();
    }

    [Fact]
    public void Highlight_InvalidColour_Clears()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply([Line(Slot, "CQ K1ABC FN42")]);
        vm.Highlight(new WsjtxHighlight("Costas", "K1ABC", new WsjtxColor(255, 255, 255, 0), new WsjtxColor(255, 0, 0, 0), false));
        vm.ApplyHighlights();

        vm.Highlight(new WsjtxHighlight("Costas", "K1ABC", null, null, false));
        vm.ApplyHighlights();

        vm.Lines.Single(l => l.IsLine).Background.Should().BeNull();
    }
}
