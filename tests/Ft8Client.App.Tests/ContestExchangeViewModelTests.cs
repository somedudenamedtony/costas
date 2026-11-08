// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using FluentAssertions;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Contests;

namespace Ft8Client.App.Tests;

public class ContestExchangeViewModelTests
{
    [Fact]
    public void Exchange_StateTyped_CanStartAndPreviewsTheMessages()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.RttyRoundup, "W7LIT", "DN40", null) { Exchange = "ut" };

        vm.CanStart.Should().BeTrue();
        vm.Result.Should().Be("UT");
        vm.Preview.Select(l => l.Text).Should().Equal("CQ RU W7LIT DN40", "‹call› W7LIT 549 UT", "‹call› W7LIT R 549 UT", "‹call› W7LIT RR73");
    }

    [Fact]
    public void Exchange_NotAState_CannotStartAndSaysWhy()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.RttyRoundup, "W7LIT", "DN40", null) { Exchange = "XX" };

        vm.CanStart.Should().BeFalse();
        vm.Problem.Should().Contain("not a state");
        vm.HasPreview.Should().BeFalse();
    }

    [Fact]
    public void Exchange_DxEntry_SendsASerial()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.RttyRoundup, "DL1ABC", "JO62", "DX");

        vm.CanStart.Should().BeTrue();
        vm.Preview[1].Text.Should().Be("‹call› DL1ABC 549 0001");
    }

    [Fact]
    public void Exchange_FieldDayClassAndSection_CanStart()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.FieldDay, "W7LIT", "DN40", "2a ut");

        vm.CanStart.Should().BeTrue();
        vm.Preview[2].Text.Should().Be("‹call› W7LIT R 2A UT");
    }

    [Fact]
    public void Exchange_FieldDayBadSection_CannotStart()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.FieldDay, "W7LIT", "DN40", "2A ZZ");

        vm.CanStart.Should().BeFalse();
        vm.Problem.Should().Contain("section");
    }

    [Fact]
    public void Exchange_GridContestWithoutGrid_CannotStart()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.WwDigi, "W7LIT", string.Empty, null);

        vm.AsksForExchange.Should().BeFalse();
        vm.CanStart.Should().BeFalse();
        vm.Problem.Should().Contain("grid");
    }

    [Fact]
    public void Exchange_NoCallsign_CannotStart()
    {
        var vm = new ContestExchangeViewModel(ContestInfo.ArrlDigi, string.Empty, "DN40", null);

        vm.CanStart.Should().BeFalse();
    }
}
