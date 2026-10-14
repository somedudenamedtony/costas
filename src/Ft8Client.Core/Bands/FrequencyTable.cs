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
    public bool TransmitAllowed(string band, Mode mode, long dialHz, int offsetHz, out string reason) =>
        TransmitAllowed(band, mode, dialHz, offsetHz, null, out reason);

    /// <summary>
    /// The band-edge guard for a signal <paramref name="widthHz"/> wide (null for the mode's usual width, an RTTY
    /// signal with a wider shift than the standard is wider). PSK and RTTY are refused on a band with no row for that
    /// mode: their limits are not borrowed from another mode's row.
    /// </summary>
    public bool TransmitAllowed(string band, Mode mode, long dialHz, int offsetHz, int? widthHz, out string reason)
    {
        var e = Find(band, mode);
        if (e is null && !ModeInfo.IsSlotted(mode) && Entries.Any(x => string.Equals(x.Band, band, StringComparison.OrdinalIgnoreCase)))
        {
            reason = $"Not sent: the frequency table has no {ModeInfo.Name(mode)} row for {BandPlan.Display(band)}, so Costas does not know its {ModeInfo.Name(mode)} limits there.";
            return false;
        }
        e ??= Entries.FirstOrDefault(x => string.Equals(x.Band, band, StringComparison.OrdinalIgnoreCase));
        if (e is null)
        {
            reason = $"No transmit limits are set for {band}.";
            return false;
        }
        // FT8 and FT4 tones sit above the offset; a PSK carrier or an RTTY signal's centre is the offset.
        var width = widthHz ?? ModeInfo.OccupiedBandwidthHz(mode);
        var low = dialHz + offsetHz - (ModeInfo.IsSlotted(mode) ? 0 : width / 2);
        var high = low + width;
        if (low < e.LowHz || high > e.HighHz)
        {
            reason = $"{low / 1000.0:0.000} kHz is outside {BandPlan.Display(e.Band)} transmit limits.";
            return false;
        }
        reason = string.Empty;
        return true;
    }
}
