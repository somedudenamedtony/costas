// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>A continent column.</summary>
/// <param name="Name">Label.</param>
/// <param name="Count">Stations.</param>
/// <param name="BarHeight">Bar height in pixels, scaled to the largest.</param>
public sealed record ContinentBar(string Name, int Count, double BarHeight);
