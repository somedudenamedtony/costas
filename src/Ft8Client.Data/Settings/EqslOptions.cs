// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Optional eQSL uploads. Credentials are in the secret store.</summary>
public sealed class EqslOptions
{
    /// <summary>Upload newly logged contacts. Off until enabled by the operator.</summary>
    public bool UploadOnComplete { get; set; }

    /// <summary>Read the inbox and match incoming cards to the local log.</summary>
    public bool SyncEnabled { get; set; }

    /// <summary>QTH nickname from eQSL, to select the correct station location.</summary>
    public string QthNickname { get; set; } = string.Empty;
}
