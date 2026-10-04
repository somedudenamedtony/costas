// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Tests;

public class MessageParserTests
{
    [Theory]
    [InlineData("CQ K1ABC FN42", null, "K1ABC", "FN42")]
    [InlineData("CQ DX VK3BMT QF22", "DX", "VK3BMT", "QF22")]
    [InlineData("CQ POTA KG5OWB EM12", "POTA", "KG5OWB", "EM12")]
    [InlineData("CQ 123 K1ABC FN42", "123", "K1ABC", "FN42")]
    [InlineData("CQ NA PY2WRA GG66", "NA", "PY2WRA", "GG66")]
    [InlineData("CQ K1ABC", null, "K1ABC", null)]
    [InlineData("CQ DX K1ABC", "DX", "K1ABC", null)]
    [InlineData("CQ UW5EJX/MM", null, "UW5EJX/MM", null)]
    [InlineData("CQ PJ4/K1ABC", null, "PJ4/K1ABC", null)]
    [InlineData("CQ RU W7BOB DN14", "RU", "W7BOB", "DN14")]
    [InlineData("cq k1abc fn42", null, "K1ABC", "FN42")]
    public void Parse_Cq_ReturnsCqWithModifierCallAndGrid(string text, string? modifier, string call, string? grid)
    {
        var m = MessageParser.Parse(text);
        m.Kind.Should().Be(MessageKind.Cq);
        m.CqModifier.Should().Be(modifier);
        m.From!.Value.Call.Should().Be(call);
        m.Grid.Should().Be(grid);
        m.To.Should().BeNull();
    }

    [Theory]
    [InlineData("K1ABC W7LIT DN40", MessageKind.GridReply, "K1ABC", "W7LIT", null)]
    [InlineData("W7LIT K1ABC -14", MessageKind.Report, "W7LIT", "K1ABC", -14)]
    [InlineData("W7LIT K1ABC +05", MessageKind.Report, "W7LIT", "K1ABC", 5)]
    [InlineData("W7LIT K1ABC -30", MessageKind.Report, "W7LIT", "K1ABC", -30)]
    [InlineData("W7LIT K1ABC +49", MessageKind.Report, "W7LIT", "K1ABC", 49)]
    [InlineData("K1ABC W7LIT R-08", MessageKind.RogerReport, "K1ABC", "W7LIT", -8)]
    [InlineData("K1ABC W7LIT R+12", MessageKind.RogerReport, "K1ABC", "W7LIT", 12)]
    [InlineData("W7LIT K1ABC RRR", MessageKind.Roger, "W7LIT", "K1ABC", null)]
    [InlineData("W7LIT K1ABC RR73", MessageKind.RogerBye, "W7LIT", "K1ABC", null)]
    [InlineData("K1ABC W7LIT 73", MessageKind.Bye, "K1ABC", "W7LIT", null)]
    [InlineData("WA9XYZ/R KA1ABC/R FN42", MessageKind.GridReply, "WA9XYZ/R", "KA1ABC/R", null)]
    [InlineData("K1ABC/P W7LIT/QRP -10", MessageKind.Report, "K1ABC/P", "W7LIT/QRP", -10)]
    [InlineData("KV4ZY NX8G 539 OH", MessageKind.Exchange, "KV4ZY", "NX8G", null)]
    [InlineData("KC8YDS KF6CRW R 559 ID", MessageKind.Exchange, "KC8YDS", "KF6CRW", null)]
    public void Parse_TwoCallMessage_ReturnsKindAndCalls(string text, MessageKind kind, string to, string from, int? report)
    {
        var m = MessageParser.Parse(text);
        m.Kind.Should().Be(kind);
        m.To!.Value.Call.Should().Be(to);
        m.From!.Value.Call.Should().Be(from);
        m.Report.Should().Be(report);
    }

    [Fact]
    public void Parse_GridReply_KeepsGrid()
    {
        MessageParser.Parse("K1ABC W7LIT DN40").Grid.Should().Be("DN40");
    }

    [Fact]
    public void Parse_Rr73InGridPosition_IsSignOffNotGrid()
    {
        var m = MessageParser.Parse("W7LIT K1ABC RR73");
        m.Kind.Should().Be(MessageKind.RogerBye);
        m.Grid.Should().BeNull();
    }

    [Fact]
    public void Parse_HashedResolvedCall_StripsBracketsAndFlags()
    {
        var m = MessageParser.Parse("<PJ4/K1ABC> W7LIT DN40");
        m.Kind.Should().Be(MessageKind.GridReply);
        m.To!.Value.Should().Be(new CallToken("PJ4/K1ABC", Hashed: true, Unresolved: false));
        m.From!.Value.Hashed.Should().BeFalse();
    }

    [Fact]
    public void Parse_UnresolvedSender_HasNoSenderCall()
    {
        var m = MessageParser.Parse("W7LIT <...> -10");
        m.Kind.Should().Be(MessageKind.Report);
        m.From!.Value.Unresolved.Should().BeTrue();
        m.SenderCall.Should().BeNull();
        m.IsAddressedTo("W7LIT").Should().BeTrue();
    }

    [Fact]
    public void Parse_UnresolvedAddressee_ParsesSender()
    {
        var m = MessageParser.Parse("<...> W7LIT DN40");
        m.SenderCall.Should().Be("W7LIT");
        m.To!.Value.Unresolved.Should().BeTrue();
    }

    [Fact]
    public void Parse_BareNonstandard_IsBare()
    {
        var m = MessageParser.Parse("<W7LIT> PJ4/K1ABC");
        m.Kind.Should().Be(MessageKind.Bare);
        m.SenderCall.Should().Be("PJ4/K1ABC");
    }

    [Fact]
    public void Parse_Dxpedition_ExpandsToRogerByeAndReport()
    {
        var m = MessageParser.Parse("K1ABC RR73; W9XYZ <KH1/KH7Z> -11");
        m.Kind.Should().Be(MessageKind.DxpeditionMulti);
        m.SenderCall.Should().Be("KH1/KH7Z");
        var parts = m.Expand();
        parts.Should().HaveCount(2);
        parts[0].Kind.Should().Be(MessageKind.RogerBye);
        parts[0].To!.Value.Call.Should().Be("K1ABC");
        parts[1].Kind.Should().Be(MessageKind.Report);
        parts[1].To!.Value.Call.Should().Be("W9XYZ");
        parts[1].Report.Should().Be(-11);
        parts.Should().OnlyContain(p => p.SenderCall == "KH1/KH7Z");
        m.IsAddressedTo("W9XYZ").Should().BeTrue();
    }

    [Theory]
    [InlineData("TNX BOB 73 GL")]
    [InlineData("TU 73")]
    [InlineData("HELLO")]
    [InlineData("")]
    [InlineData("CQ")]
    [InlineData("CQ FN42")]
    public void Parse_Other_IsFreeText(string text)
    {
        var m = MessageParser.Parse(text);
        m.Kind.Should().Be(MessageKind.FreeText);
        m.SenderCall.Should().BeNull();
    }

    [Theory]
    [InlineData("DX", true)]
    [InlineData("POTA", true)]
    [InlineData("A", true)]
    [InlineData("145", true)]
    [InlineData("1", true)]
    [InlineData("TESTS", false)]
    [InlineData("1234", false)]
    [InlineData("A1", false)]
    public void IsCqModifier_Token_MatchesRule(string token, bool expected)
    {
        MessageParser.IsCqModifier(token).Should().Be(expected);
    }
}
