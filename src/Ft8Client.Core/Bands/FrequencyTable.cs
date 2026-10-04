// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Bands;

/// <summary>The operator-editable list of dial frequencies and transmit limits.</summary>
public sealed class FrequencyTable(IReadOnlyList<FrequencyEntry> entries)
{
    /// <summary>All entries.</summary>
    public IReadOnlyList<FrequencyEntry> Entries { get; } = entries;

    /// <summary>Bands with an entry for a mode, in table order.</summary>
    public IReadOnlyList<string> Bands(Mode mode) => Entries.Where(e => e.Mode == mode).Select(e => e.Band).Distinct().ToList();

    /// <summary>The entry for a band and mode, or null.</summary>
    public FrequencyEntry? Find(string band, Mode mode) =>
        Entries.FirstOrDefault(e => e.Mode == mode && string.Equals(e.Band, band, StringComparison.OrdinalIgnoreCase));

    /// <summary>The entry whose dial frequency is nearest, within 10 kHz, for a mode.</summary>
    public FrequencyEntry? FindByDial(long dialHz, Mode mode) =>
        Entries.Where(e => e.Mode == mode && Math.Abs(e.DialHz - dialHz) <= 10_000).MinBy(e => Math.Abs(e.DialHz - dialHz));

    /// <summary>
    /// The band-edge guard: true when the whole transmitted signal (dial + offset to dial + offset + bandwidth)
    /// lies inside the allowed range for the band.
    /// </summary>
    public bool TransmitAllowed(string band, Mode mode, long dialHz, int offsetHz, out string reason)
    {
        var e = Find(band, mode) ?? Entries.FirstOrDefault(x => string.Equals(x.Band, band, StringComparison.OrdinalIgnoreCase));
        if (e is null)
        {
            reason = $"No transmit limits are set for {band}.";
            return false;
        }
        var low = dialHz + offsetHz;
        var high = low + ModeInfo.OccupiedBandwidthHz(mode);
        if (low < e.LowHz || high > e.HighHz)
        {
            reason = $"{low / 1000.0:0.000} kHz is outside {BandPlan.Display(e.Band)} transmit limits.";
            return false;
        }
        reason = string.Empty;
        return true;
    }
}
