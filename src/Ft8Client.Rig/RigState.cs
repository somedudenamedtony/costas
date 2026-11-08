// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>A reading of the radio.</summary>
/// <param name="FrequencyHz">Dial frequency.</param>
/// <param name="Mode">Mode as Hamlib names it (USB, PKTUSB).</param>
/// <param name="Ptt">PTT asserted.</param>
/// <param name="Split">Split on.</param>
/// <param name="PassbandHz">
/// Receive filter width in hertz as the radio reports it with the mode; null when it reports none (0 or missing).
/// </param>
public sealed record RigState(long FrequencyHz, string Mode, bool Ptt, bool Split, int? PassbandHz = null)
{
    /// <summary>A Hamlib passband reply as a width, or null when it is missing, zero or not a number.</summary>
    public static int? ParsePassband(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var hz) && hz is > 0 and <= 20_000
            ? hz : null;
}
