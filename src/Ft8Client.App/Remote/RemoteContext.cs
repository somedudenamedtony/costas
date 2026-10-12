// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Remote;

/// <summary>What the phone's state is built from besides the session snapshot: the same sources the Operate view reads.</summary>
public sealed record RemoteContext
{
    /// <summary>Distance units.</summary>
    public DistanceUnit Units { get; init; } = DistanceUnit.Miles;

    /// <summary>Name from a QRZ lookup, by call.</summary>
    public Func<string, string?> QrzName { get; init; } = _ => null;

    /// <summary>The QRZ lookup, by call.</summary>
    public Func<string, CallsignCacheEntry?> QrzLookup { get; init; } = _ => null;

    /// <summary>The log index, for last worked and past contacts.</summary>
    public Func<LogIndex?> Log { get; init; } = () => null;

    /// <summary>The country file, for who hears me and tonight's contacts.</summary>
    public CountryFile? Countries { get; init; }

    /// <summary>Club Log most-wanted rank, by entity key.</summary>
    public Func<string?, int?> MostWantedRank { get; init; } = _ => null;

    /// <summary>The bands the station can switch to, by mode.</summary>
    public Func<Mode, IReadOnlyList<string>> Bands { get; init; } = _ => [];
}
