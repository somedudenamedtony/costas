// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;

namespace Ft8Client.App.Tests;

public class EscapeDecisionTests
{
    private static ContactView Contact(int? sent, int? received) =>
        new("KP4JRS", null, ContactFlow.AnswerCq, ContactOutcome.InProgress, sent, received, [], false, false, string.Empty);

    private static SessionSnapshot Keyboard(bool onAir, ContactView? contact) => new()
    {
        Mode = Mode.Psk31,
        Contact = contact,
        KeyboardTx = onAir ? new KeyboardTxView(true, "KP4JRS DE", " W7LIT K", true) : KeyboardTxView.Idle,
    };

    [Fact]
    public void EscapeDecision_KeyboardOverInContact_HaltsAndArms()
    {
        MainViewModel.EscapeDecision(Keyboard(true, Contact(599, 599)), armed: false).Should().Be(MainViewModel.EscapeAction.HaltAndArm);
    }

    [Fact]
    public void EscapeDecision_SecondPressAfterKeyboardOver_Abandons()
    {
        // The over has stopped; the reports are both in, which once asked for a third press.
        MainViewModel.EscapeDecision(Keyboard(false, Contact(599, 599)), armed: true).Should().Be(MainViewModel.EscapeAction.Abandon);
    }

    [Fact]
    public void EscapeDecision_KeyboardOverWithNoContact_OnlyHalts()
    {
        MainViewModel.EscapeDecision(Keyboard(true, null), armed: false).Should().Be(MainViewModel.EscapeAction.Halt);
    }

    [Fact]
    public void EscapeDecision_BothReportsNotArmed_HaltsAndArms()
    {
        var s = new SessionSnapshot { Mode = Mode.Ft8, Contact = Contact(-10, -12) };
        MainViewModel.EscapeDecision(s, armed: false).Should().Be(MainViewModel.EscapeAction.HaltAndArm);
    }

    [Fact]
    public void EscapeDecision_NoReportsYet_Abandons()
    {
        var s = new SessionSnapshot { Mode = Mode.Ft8, Contact = Contact(null, null) };
        MainViewModel.EscapeDecision(s, armed: false).Should().Be(MainViewModel.EscapeAction.Abandon);
    }

    [Fact]
    public void EscapeText_NoContact_SaysOnlyStopped()
    {
        MainViewModel.EscapeText(null, armed: false).Should().Be("Transmitting stopped.");
    }

    [Fact]
    public void EscapeText_Armed_NamesTheContact()
    {
        MainViewModel.EscapeText(Contact(599, 599), armed: true).Should().Contain("abandon the contact with KP4JRS");
    }
}
