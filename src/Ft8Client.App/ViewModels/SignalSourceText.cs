// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;

namespace Ft8Client.App.ViewModels;

/// <summary>The caption under Your signal that says where the reports of my signal come from.</summary>
public static class SignalSourceText
{
    /// <summary>
    /// "PSK Reporter · live", "PSK Reporter · updated 6 min ago" or "PSK Reporter · unavailable", followed by
    /// " · RBN live" while Reverse Beacon Network spots are arriving. With PSK Reporter off and RBN on: "RBN · live".
    /// </summary>
    public static string For(IReadOnlyDictionary<string, ServiceStatus> services, DateTime now)
    {
        var psk = services.TryGetValue(ServiceNames.Psk, out var p) ? p : null;
        var rbn = services.TryGetValue(ServiceNames.Rbn, out var r) && r.Health == ServiceHealth.Ok;
        if (rbn && psk?.Health is null or ServiceHealth.Off) return "RBN · live";
        var text = psk?.Health switch
        {
            ServiceHealth.Ok => "PSK Reporter · live",
            ServiceHealth.Degraded => $"PSK Reporter · updated {StationText.ShortAgo(now - psk.SinceUtc)}",
            _ => "PSK Reporter · unavailable",
        };
        return rbn ? text + " · RBN live" : text;
    }
}
