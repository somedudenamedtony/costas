// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;

namespace Ft8Client.App.Services;

/// <summary>Opens web pages in the default browser.</summary>
public static class Browser
{
    /// <summary>The QRZ Logbook, where the logbook's API key is shown (its Settings).</summary>
    public const string QrzLogbook = "https://logbook.qrz.com/";

    /// <summary>Opens an https link; anything else is ignored.</summary>
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "Could not open {Url}", url);
        }
    }
}
