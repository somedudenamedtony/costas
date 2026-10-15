// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Data.Settings;

/// <summary>Anonymous usage statistics (Aptabase).</summary>
public sealed class UsageOptions
{
    /// <summary>Send anonymous usage counts. On unless the operator turns it off (opt-out).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>True once the main window has shown the notice that explains them. Nothing is sent before that.</summary>
    public bool NoticeShown { get; set; }
}
