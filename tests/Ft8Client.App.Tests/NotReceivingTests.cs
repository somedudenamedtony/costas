// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Services;

namespace Ft8Client.App.Tests;

public class NotReceivingTests
{
    private static SessionSnapshot With(ServiceHealth radio, ServiceHealth audio) => new()
    {
        Services = new Dictionary<string, ServiceStatus>
        {
            [ServiceNames.Radio] = new(radio, string.Empty, DateTime.UnixEpoch),
            [ServiceNames.Audio] = new(audio, string.Empty, DateTime.UnixEpoch),
        },
    };

    [Fact]
    public void NotReceiving_RadioDown_SaysNoRadio()
    {
        MainViewModel.NotReceiving(With(ServiceHealth.Down, ServiceHealth.Ok)).Should().Be("Not receiving · no radio");
    }

    [Fact]
    public void NotReceiving_AudioDown_SaysNoAudio()
    {
        MainViewModel.NotReceiving(With(ServiceHealth.Ok, ServiceHealth.Down)).Should().Be("Not receiving · no audio");
    }

    [Fact]
    public void NotReceiving_RadioAndAudioOk_ReturnsNull()
    {
        MainViewModel.NotReceiving(With(ServiceHealth.Ok, ServiceHealth.Ok)).Should().BeNull();
    }

    [Fact]
    public void NotReceiving_NoServicesReported_SaysNoRadio()
    {
        MainViewModel.NotReceiving(new SessionSnapshot()).Should().Be("Not receiving · no radio");
    }
}
