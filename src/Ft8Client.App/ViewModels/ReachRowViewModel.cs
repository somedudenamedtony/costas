// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>A row of Who heard you.</summary>
/// <param name="Station">Call.</param>
/// <param name="Where">Place.</param>
/// <param name="Distance">Distance text.</param>
/// <param name="Bearing">Bearing.</param>
/// <param name="Db">Report.</param>
/// <param name="Age">Age.</param>
/// <param name="InLog">Needed or Worked.</param>
/// <param name="Km">For sorting.</param>
public sealed record ReachRowViewModel(string Station, string Where, string Distance, string Bearing, string Db, string Age, string InLog, double Km)
{
    /// <summary>Needed shows in accent.</summary>
    public TextKind InLogKind => InLog == "Needed" ? TextKind.Accent : TextKind.Secondary;
}
