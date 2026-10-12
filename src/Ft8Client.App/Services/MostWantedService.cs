// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Time;
using Ft8Client.Data;
using Ft8Client.Data.Reference;
using Ft8Client.Integrations.ClubLog;
using Ft8Client.Integrations.Net;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Keeps Club Log's most-wanted list for the rarity meter: loads the cached copy at start and downloads a new one when
/// it is a week old. Optional: without it the meter is simply not shown.
/// </summary>
public sealed class MostWantedService
{
    /// <summary>How old the cached list may get before it is downloaded again.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    private readonly string _cacheFile;
    private readonly Func<string> _callsign;
    private readonly IClock _clock;
    private readonly HttpMessageHandler? _handler;
    private IReadOnlyDictionary<int, string>? _keys;

    /// <summary>Creates the service.</summary>
    public MostWantedService(AppPaths paths, Func<string> callsign, IClock clock, HttpMessageHandler? handler = null)
    {
        _cacheFile = Path.Combine(paths.Cache, "clublog-mostwanted.json");
        _callsign = callsign;
        _clock = clock;
        _handler = handler;
    }

    /// <summary>The list in use; <see cref="MostWanted.None"/> until one is loaded.</summary>
    public MostWanted Current { get; private set; } = MostWanted.None;

    /// <summary>Raised when a new list is in use.</summary>
    public event Action<MostWanted>? Changed;

    /// <summary>Loads the cached list, then downloads a fresh one in the background if it is missing or old.</summary>
    public Task StartAsync()
    {
        try
        {
            _keys = ReferenceData.LoadAdifTable();
            if (File.Exists(_cacheFile) && ClubLogMostWantedClient.Parse(File.ReadAllText(_cacheFile)) is { } ranks)
                Use(MostWanted.Build(ranks, _keys, File.GetLastWriteTimeUtc(_cacheFile)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            Log.Warning(ex, "Could not load the cached most-wanted list");
        }
        return _keys is not null && _clock.UtcNow - Current.FetchedUtc >= MaxAge ? Task.Run(RefreshAsync) : Task.CompletedTask;
    }

    private async Task RefreshAsync()
    {
        using var http = HttpFactory.Create(_callsign(), _handler);
        var result = await new ClubLogMostWantedClient(http).FetchAsync(CancellationToken.None).ConfigureAwait(false);
        if (result is not { } r)
        {
            Log.Information("Club Log most-wanted list unavailable; the rarity meter uses {Fetched}", Current.Count > 0 ? "the cached copy" : "nothing");
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
            File.WriteAllText(_cacheFile, r.Json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not cache the most-wanted list");
        }
        Use(MostWanted.Build(r.AdifByRank, _keys!, _clock.UtcNow));
    }

    private void Use(MostWanted list)
    {
        Current = list;
        Changed?.Invoke(list);
    }
}
