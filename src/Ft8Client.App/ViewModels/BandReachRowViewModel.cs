// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
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

/// <summary>A row of By band.</summary>
/// <param name="Band">Band.</param>
/// <param name="Stations">Stations that heard me.</param>
/// <param name="Farthest">Farthest.</param>
/// <param name="Median">Median report.</param>
/// <param name="Current">Current band (highlighted).</param>
public sealed record BandReachRowViewModel(string Band, string Stations, string Farthest, string Median, bool Current);
