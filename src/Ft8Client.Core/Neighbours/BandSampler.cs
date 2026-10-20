// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>
/// Takes turns listening to the bands that are not followed all the time, one band at a time, so the band menu has
/// numbers for every band without asking PSK Reporter for every report on every band (docs/04-domain-logic.md, section 14).
/// A sample lasts <see cref="Dwell"/>, or ends sooner after <see cref="MaxReports"/> reports; the next starts no sooner
/// than <see cref="Dwell"/> after the last one started, so at most <see cref="MaxReports"/> arrive per <see cref="Dwell"/>.
/// Not thread-safe.
/// </summary>
/// <param name="dwell">How long each band is listened to.</param>
public sealed class BandSampler(TimeSpan dwell)
{
    /// <summary>How long each band is listened to by default.</summary>
    public static readonly TimeSpan DefaultDwell = TimeSpan.FromSeconds(30);

    /// <summary>Creates a sampler that listens to each band for <see cref="DefaultDwell"/>.</summary>
    public BandSampler()
        : this(DefaultDwell)
    {
    }

    /// <summary>How long each band is listened to.</summary>
    public TimeSpan Dwell { get; } = dwell;

    /// <summary>Reports (of any station in the entity) after which a sample ends early.</summary>
    public const int MaxReports = 1000;

    private readonly List<NeighbourReport> _reports = [];
    private int _next;
    private int _count;
    private DateTime _startUtc;
    private DateTime _lastStartUtc = DateTime.MinValue;
    private string _mode = string.Empty;

    /// <summary>The band being listened to, lower case, or null between samples.</summary>
    public string? Band { get; private set; }

    /// <summary>
    /// Counts one report that arrived for the sampled band; <paramref name="report"/> is kept when given (null for a
    /// report without a sender locator). Returns false when no sample is running or it is full.
    /// </summary>
    public bool Add(NeighbourReport? report)
    {
        if (Band is null || _count >= MaxReports) return false;
        _count++;
        if (report is not null) _reports.Add(report);
        return true;
    }

    /// <summary>
    /// Moves the rotation on: ends the running sample when its time is up, it is full, its band is <paramref name="skip"/>
    /// (followed all the time now), is no longer in <paramref name="bands"/>, or the mode changed; then starts the next band
    /// when one is due. Returns the finished sample (null when none) and whether <see cref="Band"/> changed.
    /// </summary>
    /// <param name="nowUtc">Now.</param>
    /// <param name="bands">The bands to take turns on; empty stops sampling.</param>
    /// <param name="skip">The band followed all the time, which is not sampled; null for none.</param>
    /// <param name="mode"><c>FT8</c> or <c>FT4</c>.</param>
    public (BandSample? Finished, bool Changed) Tick(DateTime nowUtc, IReadOnlyList<string> bands, string? skip, string mode)
    {
        BandSample? finished = null;
        var changed = false;
        if (Band is { } band)
        {
            var modeChanged = !string.Equals(mode, _mode, StringComparison.Ordinal);
            var gone = !bands.Any(b => string.Equals(b, band, StringComparison.OrdinalIgnoreCase));
            if (modeChanged || gone || string.Equals(band, skip, StringComparison.OrdinalIgnoreCase) || _count >= MaxReports
                || nowUtc - _startUtc >= Dwell)
            {
                // A sample cut short by a mode change measured the old mode, so it is dropped.
                if (!modeChanged && nowUtc > _startUtc) finished = new BandSample(band, _mode, _startUtc, nowUtc, [.. _reports]);
                Band = null;
                _reports.Clear();
                _count = 0;
                changed = true;
            }
        }
        if (Band is null && nowUtc - _lastStartUtc >= Dwell)
        {
            var candidates = bands.Where(b => !string.Equals(b, skip, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count > 0)
            {
                Band = candidates[_next++ % candidates.Count].ToLowerInvariant();
                _mode = mode;
                _startUtc = nowUtc;
                _lastStartUtc = nowUtc;
                changed = true;
            }
        }
        return (finished, changed);
    }

    /// <summary>Stops sampling; the running sample is dropped.</summary>
    /// <returns>True when a sample was running.</returns>
    public bool Stop()
    {
        if (Band is null) return false;
        Band = null;
        _reports.Clear();
        _count = 0;
        return true;
    }
}
