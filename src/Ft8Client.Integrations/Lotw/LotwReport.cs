// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Adif;

namespace Ft8Client.Integrations.Lotw;

/// <summary>The answer to a LoTW confirmation download.</summary>
/// <param name="Records">The QSL records (one per confirmed contact), or empty on failure.</param>
/// <param name="LastQsl">The report's <c>APP_LoTW_LASTQSL</c> (when the newest confirmation in it arrived), to ask from next time.</param>
/// <param name="Error">Why the download failed, or null.</param>
public sealed record LotwReport(IReadOnlyList<AdifRecord> Records, string? LastQsl, string? Error)
{
    /// <summary>True when the download worked.</summary>
    public bool Ok => Error is null;
}
