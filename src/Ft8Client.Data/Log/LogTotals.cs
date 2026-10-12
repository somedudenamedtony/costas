// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Data.Log;

/// <summary>Totals of the contacts a <see cref="LogFilter"/> matches, for the Log view's status line.</summary>
/// <param name="Contacts">Contacts matched.</param>
/// <param name="Countries">Distinct countries among them.</param>
/// <param name="Confirmed">How many of them are confirmed.</param>
public sealed record LogTotals(int Contacts, int Countries, int Confirmed);
