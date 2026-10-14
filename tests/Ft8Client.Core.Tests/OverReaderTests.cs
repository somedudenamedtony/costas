// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Keyboard;

namespace Ft8Client.Core.Tests;

public class OverReaderTests
{
    [Theory]
    [InlineData("CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n", "KP4JRS")]
    [InlineData("cq cq ja7qvi ja7qvi ", "JA7QVI")]
    [InlineData("CQ CQ DX DE EA8TL EA8TL ", "EA8TL")]
    [InlineData("QRZ? DE 3D2AG K ", "3D2AG")]
    public void Read_Cq_FindsCaller(string text, string call)
    {
        var o = OverReader.Read(text);
        o.Sender.Should().Be(call);
        o.Cq.Should().BeTrue();
        o.To.Should().BeNull();
    }

    [Fact]
    public void Read_ADeB_SenderIsBAndToIsA()
    {
        var o = OverReader.Read("W7LIT DE EA8TL GE TONY UR 599 599 NAME JUAN HW? W7LIT DE EA8TL KN ");
        o.Sender.Should().Be("EA8TL");
        o.To.Should().Be("W7LIT");
        o.Cq.Should().BeFalse();
        o.Ended.Should().BeTrue();
    }

    [Fact]
    public void Read_RepeatedCallBeforeK_IsSender()
    {
        var o = OverReader.Read("TNX FER QSO 73 ES GL 2E0ABC 2E0ABC SK\n");
        o.Sender.Should().Be("2E0ABC");
        o.Ended.Should().BeTrue();
    }

    [Theory]
    [InlineData("UR RST 5NN 5NN NAME BOB")]
    [InlineData("73S ES TNX 73S ES TNX K ")]
    [InlineData("QTH NR DN40 DN40 K ")]
    [InlineData("THE WX HR IS SUNNY")]
    public void Read_NoCallPattern_NoSender(string text)
    {
        OverReader.Read(text).Sender.Should().BeNull();
    }

    [Theory]
    [InlineData("MY QRZ.COM PAGE IS UP TO DATE ")]
    [InlineData("TNX FER ANS MY CQ. UR RST 599 ")]
    [InlineData("W7LIT DE EA8TL QRZ COM ")]
    public void Read_CqOrQrzMentioned_NotACall(string text) => OverReader.Read(text).Cq.Should().BeFalse();

    [Theory]
    [InlineData("CQ TEST W7LIT W7LIT ")]
    [InlineData("QRZ DE W7LIT ")]
    public void Read_CqForms_Cq(string text) => OverReader.Read(text).Cq.Should().BeTrue();

    [Fact]
    public void Read_BackToYouAndCallTwice_CallIsAddressee()
    {
        var o = OverReader.Read("TNX FER INFO BTU N0XYZ N0XYZ K ");
        o.Sender.Should().BeNull();
        o.To.Should().Be("N0XYZ");
    }

    [Fact]
    public void Read_EndsWithSk_SignedOff()
    {
        OverReader.Read("73 W7LIT DE EA8TL SK ").SignedOff.Should().BeTrue();
        OverReader.Read("W7LIT DE EA8TL KN ").SignedOff.Should().BeFalse();
    }

    [Fact]
    public void Read_CallNotInPattern_NotSender()
    {
        OverReader.Read("I WORKED JA1ABC YESTERDAY ").Sender.Should().BeNull();
    }

    [Theory]
    [InlineData("CQ CQ DE KP4J")]
    [InlineData("CQ CQ DE K")]
    public void Read_LastWordIncomplete_NotRead(string text)
    {
        var o = OverReader.Read(text);
        o.Sender.Should().BeNull();
        o.Ended.Should().BeFalse();
    }

    [Fact]
    public void Read_UnfinishedOver_NotEnded()
    {
        OverReader.Read("W7LIT DE EA8TL GE").Ended.Should().BeFalse();
    }

    [Theory]
    [InlineData("W7LIT", true)]
    [InlineData("KP4JRS/P", true)]
    [InlineData("EA8/W7LIT", true)]
    [InlineData("5NN", false)]
    [InlineData("73S", false)]
    [InlineData("DN40", false)]
    [InlineData("599", false)]
    public void IsCall_Word_Result(string word, bool expected)
    {
        OverReader.IsCall(word).Should().Be(expected);
    }
}
