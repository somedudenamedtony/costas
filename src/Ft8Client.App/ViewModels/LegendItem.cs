// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One line of a map legend: a dot (or ring) in a colour, and what it means in words.</summary>
/// <param name="Kind">Colour token.</param>
/// <param name="Text">Meaning.</param>
/// <param name="Ring">Hollow ring rather than a filled dot.</param>
/// <param name="Opacity">Dot opacity.</param>
public sealed record LegendItem(TextKind Kind, string Text, bool Ring = false, double Opacity = 1);
