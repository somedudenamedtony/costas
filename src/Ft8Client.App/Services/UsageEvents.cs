// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Globalization;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Services;

/// <summary>
/// The properties sent with usage events. Only choices and counts: never a callsign, grid, name, frequency, port,
/// address or anything from the log.
/// </summary>
public static class UsageEvents
{
    /// <summary>"app_started": how the station is set up and which optional features are on.</summary>
    public static Dictionary<string, object> Started(AppSettings s, bool decoderFound)
    {
        var rig = s.Profile.Rig;
        var p = new Dictionary<string, object>
        {
            ["rig"] = rig.Mode,
            ["ptt"] = rig.Mode == RigModes.Hamlib ? rig.Ptt : "none",
            ["decoder"] = decoderFound ? "found" : "missing",
            ["setup_complete"] = OnOff(s.SetupComplete),
            ["mode"] = s.Operating.Mode,
            ["band"] = s.Operating.Band,
            ["theme"] = s.Appearance.Theme,
            ["units"] = s.Appearance.Units,
            ["qrz_logbook"] = OnOff(s.Qrz.LogbookEnabled),
            ["lotw"] = OnOff(s.Lotw.Enabled),
            ["eqsl"] = OnOff(s.Profile.Eqsl.UploadOnComplete || s.Profile.Eqsl.SyncEnabled),
            ["psk_feed"] = OnOff(s.PskReporter.FeedEnabled),
            ["psk_upload"] = OnOff(s.PskReporter.UploadSpots),
            ["rbn"] = OnOff(s.ReverseBeacon.Enabled),
            ["pota"] = OnOff(s.Pota.SpotsEnabled),
            ["udp"] = OnOff(s.Udp.Enabled),
            ["remote"] = OnOff(s.Remote.Enabled),
            ["hunt_entries"] = s.Hunt.Targets.Count,
            ["auto_update"] = OnOff(s.Updates.CheckEnabled),
        };
        // The Hamlib model number says which radios Costas is used with; it is the same for every owner of that model.
        if (rig.Mode == RigModes.Hamlib && rig.Model is { } model) p["rig_model"] = model.ToString(CultureInfo.InvariantCulture);
        return p;
    }

    /// <summary>A band and mode, such as 20m and FT8.</summary>
    public static Dictionary<string, object> BandMode(string band, string mode) => new()
    {
        ["band"] = band,
        ["mode"] = mode,
    };

    private static string OnOff(bool on) => on ? "on" : "off";
}
