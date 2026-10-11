// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;

namespace Ft8Client.App.Tests;

public class ContactPanelViewModelTests
{
    private static readonly DateTime Slot = new(2026, 10, 11, 20, 29, 30, DateTimeKind.Utc);
    private static readonly DateTime SentAt = Slot.AddSeconds(0.5);

    // Answering N4OPI's CQ: my grid reply has gone out (the engine counts it as sent when it starts) and their report is due.
    private static ContactView AfterGridReply() => new("N4OPI", "EM73", ContactFlow.AnswerCq, ContactOutcome.InProgress, null, null,
    [
        new ContactStep(true, "N4OPI W7LIT DN40", StepState.Done, 1, SentAt),
        new ContactStep(false, "W7LIT N4OPI ±NN", StepState.Now, 0, null),
        new ContactStep(true, "N4OPI W7LIT R-14", StepState.Upcoming, 0, null),
        new ContactStep(false, "W7LIT N4OPI RR73", StepState.Upcoming, 0, null),
        new ContactStep(true, "N4OPI W7LIT 73", StepState.Upcoming, 0, null),
    ], false, false, string.Empty);

    private static ContactPanelViewModel Panel(ContactView c, bool transmitting, string? onAir, DateTime now) =>
        ContactPanelViewModel.From(c, new SessionSnapshot { Mode = Mode.Ft8, Contact = c, Transmitting = transmitting, TransmittingMessage = onAir },
            now, DistanceUnit.Miles, null);

    [Fact]
    public void From_MessageStillOnAir_ShowsItSendingAndReplyNotYetWaitedFor()
    {
        var p = Panel(AfterGridReply(), true, "N4OPI W7LIT DN40", Slot.AddSeconds(4));

        p.Steps[0].Look.Should().Be(StepLook.Transmitting);
        p.Steps[0].Time.Should().Be("20:29:30");
        p.Steps[0].Status.Should().Be("Sending · 10 s left");
        p.Steps[1].Look.Should().Be(StepLook.Upcoming);
        p.Steps[1].Time.Should().Be("20:29:45");
        p.Steps[1].Status.Should().Be("Expected");
    }

    [Fact]
    public void From_TransmissionEnded_ShowsSentAndWaitingForReply()
    {
        var p = Panel(AfterGridReply(), false, null, Slot.AddSeconds(14));

        p.Steps[0].Look.Should().Be(StepLook.Done);
        p.Steps[0].Status.Should().Be("Sent");
        p.Steps[1].Look.Should().Be(StepLook.Waiting);
        p.Steps[1].Time.Should().Be("20:29:45");
        p.Steps[1].Status.Should().Be("Waiting for reply");
    }

    [Fact]
    public void From_DuringTheirSlot_KeepsExpectedTimeOfThatSlot()
    {
        var p = Panel(AfterGridReply(), false, null, Slot.AddSeconds(20));

        p.Steps[1].Time.Should().Be("20:29:45");
    }

    [Fact]
    public void From_OtherMessageOnAir_DoesNotMarkStepSending()
    {
        var p = Panel(AfterGridReply(), true, "CQ W7LIT DN40", Slot.AddSeconds(4));

        p.Steps.Should().NotContain(s => s.Look == StepLook.Transmitting);
        p.Steps[1].Look.Should().Be(StepLook.Waiting);
    }

    [Fact]
    public void From_SameMessageSentInEarlierSlot_IsNotSendingNow()
    {
        var p = Panel(AfterGridReply(), true, "N4OPI W7LIT DN40", Slot.AddSeconds(34));

        p.Steps[0].Look.Should().Be(StepLook.Done);
    }

    [Fact]
    public void From_UpcomingSteps_LabelNextAndThenLogged()
    {
        var p = Panel(AfterGridReply(), false, null, Slot.AddSeconds(14));

        p.Steps[2].Status.Should().Be("Next");
        p.Steps[2].CanJump.Should().BeTrue();
        p.Steps[3].Status.Should().Be("Expected");
        p.Steps[4].Status.Should().Be("Then logged");
        p.Steps.Select(s => s.Who).Should().Equal("You", "N4OPI", "You", "N4OPI", "You");
    }
}
