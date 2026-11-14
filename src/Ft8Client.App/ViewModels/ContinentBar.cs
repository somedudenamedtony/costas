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
/// <param name="Code">Two-letter continent code ("NA", "EU"), the label under the small bars of the idle strip.</param>
public sealed record ContinentBar(string Name, int Count, double BarHeight, string Code = "")
{
    /// <summary>Tallest bar of the idle strip's small chart, in pixels.</summary>
    public const double MiniMax = 14;

    /// <summary>Bar height in the idle strip's small chart: scaled like <see cref="BarHeight"/>, and never invisible while anything is heard.</summary>
    public double MiniHeight => Count <= 0 ? 1 : Math.Max(2, Math.Round(BarHeight * MiniMax / 48.0));

    /// <summary>"Europe: 8 stations", for the tooltip and screen readers.</summary>
    public string Tip => $"{Name}: {Count} {(Count == 1 ? "station" : "stations")}";
}
