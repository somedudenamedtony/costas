// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.ViewModels;

/// <summary>One multiplier in the Contest tab's grid: a state, province or field, and whether it is worked.</summary>
/// <param name="Name">The abbreviation.</param>
/// <param name="Worked">True when worked.</param>
public sealed record ContestMultChip(string Name, bool Worked);
