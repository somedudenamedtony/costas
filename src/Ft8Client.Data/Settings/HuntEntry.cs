// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>One Hunt list entry as stored.</summary>
public sealed class HuntEntry
{
    /// <summary><c>country</c>, <c>call</c>, <c>state</c>, <c>grid</c> or <c>newcountry</c>.</summary>
    public string Kind { get; set; } = "country";

    /// <summary>Entity key, call, state code or grid; empty for <c>newcountry</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>ADIF band ("40m"), or null for any band.</summary>
    public string? Band { get; set; }
}
