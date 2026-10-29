// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text.Json;
using Ft8Client.App.Services;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Tests;

public class UsageEventsTests
{
    [Fact]
    public void Started_StationWithCallGridAndPort_SendsNoneOfThem()
    {
        var s = new AppSettings();
        s.Profile.Callsign = "W7LIT";
        s.Profile.Grid = "DN40";
        s.Profile.Rig.Mode = RigModes.Hamlib;
        s.Profile.Rig.Model = 1049;
        s.Profile.Rig.Port = "COM3";

        var json = JsonSerializer.Serialize(UsageEvents.Started(s, decoderFound: true));

        json.Should().NotContain("W7LIT").And.NotContain("DN40").And.NotContain("COM3");
        json.Should().Contain("\"rig\":\"hamlib\"").And.Contain("\"rig_model\":\"1049\"");
    }
}
