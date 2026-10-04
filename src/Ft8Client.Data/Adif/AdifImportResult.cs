// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Adif;

/// <summary>Counts from an ADIF import.</summary>
/// <param name="Imported">New contacts.</param>
/// <param name="Merged">Duplicates merged into existing contacts.</param>
/// <param name="Skipped">Records skipped (no call or no date).</param>
public sealed record AdifImportResult(int Imported, int Merged, int Skipped);
