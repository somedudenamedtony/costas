// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.App.Remote;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Keyboard;

namespace Ft8Client.App.Tests;

// The contact card and the phone for a PSK or RTTY contact.
public class KeyboardContactPanelTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc);

    private static ContactView Contact(ContactOutcome outcome = ContactOutcome.InProgress, Mode? mode = null) =>
        new("KP4JRS", null, ContactFlow.AnswerCq, outcome, 599, 579,
        [
            new ContactStep(false, "CQ CQ DE KP4JRS", StepState.Done, 1, Now.AddMinutes(-2)),
            new ContactStep(true, "KP4JRS DE W7LIT W7LIT K", StepState.Done, 1, Now.AddMinutes(-1)),
            new ContactStep(false, "UR RST", StepState.Now, 0, null),
            new ContactStep(true, "EXCHANGE", StepState.Upcoming, 0, null),
        ], false, false, string.Empty) { Mode = mode };

    private static SessionSnapshot Snap(ContactView c, Mode mode = Mode.Psk31, Exchange? received = null) =>
        new() { MyCall = "W7LIT", Band = "20m", Mode = mode, Contact = c, Received = received };

    [Fact]
    public void For_KeyboardContact_ShowsRstWithoutSign()
    {
        var p = ContactPanelViewModel.For(Contact(), Snap(Contact()), Now, DistanceUnit.Miles, null);

        p.Sent.Should().Be("599");
        p.Received.Should().Be("579");
        p.Caption.Should().StartWith("PSK31 · ");
    }

    [Fact]
    public void For_ContactMadeInPsk31ShownInRtty_KeepsItsMode()
    {
        var c = Contact(ContactOutcome.Abandoned, Mode.Psk31);

        ContactPanelViewModel.For(c, Snap(c, Mode.Rtty), Now, DistanceUnit.Miles, null).Caption.Should().Be("PSK31 · Abandoned");
    }

    [Fact]
    public void FromKeyboard_Abandoned_WaitsForNothing()
    {
        var c = Contact(ContactOutcome.Abandoned);

        var p = ContactPanelViewModel.FromKeyboard(c, Snap(c), DistanceUnit.Miles, null);

        p.Steps.Should().NotContain(x => x.Look == StepLook.Waiting);
        p.Steps[2].Status.Should().Be("Not received");
    }

    [Fact]
    public void FromKeyboard_NameAndQth_Shown()
    {
        var c = Contact();

        var p = ContactPanelViewModel.FromKeyboard(c, Snap(c, received: new Exchange(579, "JOSE", "SAN JUAN", null)), DistanceUnit.Miles, null);

        p.Subline.Should().Contain("Name JOSE").And.Contain("QTH SAN JUAN");
    }

    [Fact]
    public void Build_KeyboardContactOnPhone_UsesKeyboardCard()
    {
        var c = Contact();

        var s = RemoteStateBuilder.Build(Snap(c), Now, new RemoteContext(), "you", null);

        s.Contact!.Sent.Should().Be("599");
        s.Contact.Caption.Should().StartWith("PSK31");
        s.SlotLengthMs.Should().Be(0);
    }
}
