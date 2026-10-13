// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>LoTW: confirmations, uploads through TQSL, and the list of LoTW users. The password is in the credential store.</summary>
public sealed class LotwOptions
{
    /// <summary>Download LoTW confirmations.</summary>
    public bool Enabled { get; set; }

    /// <summary>LoTW login name (usually the callsign).</summary>
    public string? Username { get; set; }

    /// <summary>Hours between confirmation downloads.</summary>
    public int DownloadHours { get; set; } = 12;

    /// <summary>Path to <c>tqsl.exe</c>; null to look in the usual install folders.</summary>
    public string? TqslPath { get; set; }

    /// <summary>The TQSL Station Location to sign with; null when TQSL has only one.</summary>
    public string? StationLocation { get; set; }

    /// <summary>Sign and upload each contact through TQSL when it is logged.</summary>
    public bool UploadOnComplete { get; set; }

    /// <summary>Download ARRL's list of LoTW users once a week (for "Uses LoTW").</summary>
    public bool UserList { get; set; } = true;
}
