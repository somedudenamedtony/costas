// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Optional uploads to a Wavelog or Cloudlog logbook. The API key is in the secret store.</summary>
public sealed class CloudlogOptions
{
    /// <summary>Upload each newly logged contact. Off until the operator turns it on.</summary>
    public bool UploadOnComplete { get; set; }

    /// <summary>The logbook site's address, as typed ("https://log.example.com").</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The station location id in that logbook the contacts go to.</summary>
    public string StationId { get; set; } = string.Empty;
}
