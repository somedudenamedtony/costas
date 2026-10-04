// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>QRZ settings. The key, username and password are in the secret store.</summary>
public sealed class QrzOptions
{
    /// <summary>Sync and upload to the logbook.</summary>
    public bool LogbookEnabled { get; set; } = true;

    /// <summary>XML lookups.</summary>
    public bool LookupEnabled { get; set; } = true;

    /// <summary>Minutes between syncs.</summary>
    public int SyncMinutes { get; set; } = 15;

    /// <summary>Upload each contact on completion.</summary>
    public bool UploadOnComplete { get; set; } = true;
}
