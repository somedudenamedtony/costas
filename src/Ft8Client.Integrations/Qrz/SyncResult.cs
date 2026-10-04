// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>Outcome of a logbook sync.</summary>
/// <param name="Fetched">Records received.</param>
/// <param name="Inserted">New contacts stored.</param>
/// <param name="Merged">Records merged into existing contacts.</param>
/// <param name="Full">True for a first (full) sync.</param>
public sealed record SyncResult(int Fetched, int Inserted, int Merged, bool Full)
{
    /// <summary>True when the log changed and the index should be rebuilt.</summary>
    public bool Changed => Inserted > 0 || Merged > 0;
}
