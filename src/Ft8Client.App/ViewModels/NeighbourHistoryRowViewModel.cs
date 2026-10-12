// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One continent in the Reach view's history of the comparison with stations near me.</summary>
/// <param name="Sentence">"Into Europe: 2 dB behind neighbours since your change on 9 Oct, 7 dB behind in the two weeks before."</param>
/// <param name="Days">Median difference per day, oldest first, null where nothing was measured.</param>
/// <param name="MarkIndex">The marked day's index in <paramref name="Days"/>, or -1.</param>
/// <param name="Tip">Chart tooltip.</param>
public sealed record NeighbourHistoryRowViewModel(string Sentence, IReadOnlyList<int?> Days, int MarkIndex, string Tip);
