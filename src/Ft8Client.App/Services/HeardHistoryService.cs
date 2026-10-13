// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.History;
using Ft8Client.Core.Time;
using Ft8Client.Data.Journal;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// The operator's receive history by country ("when to look"), read from the decode journal on a background thread
/// and cached. The journal can be tens of megabytes, so it is streamed and only re-read when the cached copy is old.
/// </summary>
public sealed class HeardHistoryService
{
    /// <summary>How old the cached history may be before opening the Awards view reads the journal again.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    private readonly DecodeJournal _journal;
    private readonly CountryFile _countries;
    private readonly IClock _clock;
    private int _reading;

    /// <summary>Creates the service.</summary>
    /// <param name="journalFolder">The data folder's journal folder.</param>
    /// <param name="countries">Maps each heard station to its country.</param>
    /// <param name="clock">For the age of the cached history.</param>
    public HeardHistoryService(string journalFolder, CountryFile countries, IClock clock)
    {
        _journal = new DecodeJournal(journalFolder);
        _countries = countries;
        _clock = clock;
    }

    /// <summary>The last history read, or null before the first read finishes. Read-only once published.</summary>
    public ReceiveHistory? Current { get; private set; }

    /// <summary>When <see cref="Current"/> was read, UTC.</summary>
    public DateTime? ReadUtc { get; private set; }

    /// <summary>True while the journal is being read.</summary>
    public bool Reading => Volatile.Read(ref _reading) == 1;

    /// <summary>Why the last read failed, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>Raised (on a background thread) when a read starts or finishes.</summary>
    public event Action? Changed;

    /// <summary>Starts a background read when there is no history yet or it is older than <paramref name="maxAge"/>.</summary>
    public Task RefreshIfOlderThan(TimeSpan maxAge)
    {
        if (ReadUtc is { } t && _clock.UtcNow - t < maxAge) return Task.CompletedTask;
        return Refresh();
    }

    /// <summary>Starts a background read unless one is already running.</summary>
    public Task Refresh()
    {
        if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0) return Task.CompletedTask;
        Changed?.Invoke();
        return Task.Run(Read);
    }

    /// <summary>The "when to look" sentence for a country, or null when it has never been heard (or nothing is read yet).</summary>
    public string? Describe(string? entityKey, bool withName = false)
    {
        var h = Current?.For(entityKey);
        if (h is null) return null;
        return WhenToLook.Describe(h, _clock.UtcNow, withName ? _countries.FindByKey(entityKey)?.Name : null);
    }

    private void Read()
    {
        try
        {
            var history = new ReceiveHistory(_countries);
            long lines = 0;
            foreach (var line in _journal.ReadLines())
            {
                history.AddLine(line);
                lines++;
            }
            Current = history;
            ReadUtc = _clock.UtcNow;
            Error = null;
            Log.Information("Read {Lines} decode-journal lines: {Countries} countries heard", lines, history.Countries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = "Could not read the decode journal: " + ex.Message;
            ReadUtc = _clock.UtcNow;
            Log.Warning(ex, "Could not read the decode journal");
        }
        finally
        {
            Volatile.Write(ref _reading, 0);
            Changed?.Invoke();
        }
    }
}
