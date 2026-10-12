// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.App.ViewModels;

/// <summary>One continent chip in a band menu row.</summary>
/// <param name="Label">"EU".</param>
/// <param name="Level">Shade: 0 for no spots, up to 3 for many.</param>
/// <param name="Tip">"EU · 12 spots".</param>
public sealed record ContinentChip(string Label, int Level, string Tip);
