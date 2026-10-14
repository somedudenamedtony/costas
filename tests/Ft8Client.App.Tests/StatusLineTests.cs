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
using Ft8Client.Modems.Rtty;

namespace Ft8Client.App.Tests;

public class StatusLineTests
{
    private static SessionSnapshot InContact(bool transmitting, string? onAir) => new()
    {
        Contact = new ContactView("K1ABC", "FN42", ContactFlow.CalledCq, ContactOutcome.InProgress, null, null, [], false, false, string.Empty),
        Transmitting = transmitting,
        TransmittingMessage = onAir,
    };

    [Fact]
    public void Status_SendingToPartner_SaysTransmittingTo()
    {
        MainViewModel.Status(InContact(true, "K1ABC W7LIT +25")).Item1.Should().Be("Transmitting to K1ABC.");
    }

    [Fact]
    public void Status_CqStillOnAirAfterLateAnswer_SaysWhatIsOnAir()
    {
        MainViewModel.Status(InContact(true, "CQ W7LIT DN40")).Item1.Should().Be("Sending CQ W7LIT DN40; answering K1ABC next slot.");
    }

    [Fact]
    public void Status_NotTransmitting_SaysWaiting()
    {
        MainViewModel.Status(InContact(false, null)).Item1.Should().Be("Waiting for K1ABC.");
    }

    [Fact]
    public void Status_ReadingRtty_NamesSpeedShiftAndReverse()
    {
        var format = new RttyFormat(45.45, 170, Reverse: true);
        MainViewModel.Status(new SessionSnapshot { Mode = Mode.Rtty }, format).Item1
            .Should().StartWith("Reading RTTY 45.45 Bd, 170 Hz shift, reversed.");
    }
}
