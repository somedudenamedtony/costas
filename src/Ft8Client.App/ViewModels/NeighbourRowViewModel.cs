// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One continent in "Compared with stations near you".</summary>
/// <param name="Toward">"Europe".</param>
/// <param name="Receivers">Shared receivers.</param>
/// <param name="Mine">My median report there.</param>
/// <param name="Theirs">The nearby stations' median report there.</param>
/// <param name="Difference">"7 dB weaker", "Level".</param>
/// <param name="DiffDb">The difference in dB, negative when I am weaker (for the bar).</param>
public sealed record NeighbourRowViewModel(string Toward, string Receivers, string Mine, string Theirs, string Difference, int DiffDb)
{
    /// <summary>True when I am weaker by enough to call out.</summary>
    public bool Weaker => DiffDb <= -Core.Neighbours.NeighbourText.LevelDb;
}
