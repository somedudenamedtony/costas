// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Text.RegularExpressions;

namespace Ft8Client.Audio.Backends;

/// <summary>Matches a stored audio device id against the devices present now.</summary>
internal static partial class AlsaDeviceIds
{
    /// <summary>
    /// The device a stored id names: an exact match, or else the same name with a different ALSA card number (USB
    /// sound cards can change number between restarts), or -1.
    /// </summary>
    public static int Find(IReadOnlyList<string> ids, string id)
    {
        for (var i = 0; i < ids.Count; i++) if (string.Equals(ids[i], id, StringComparison.Ordinal)) return i;
        var loose = WithoutCardNumber(id);
        for (var i = 0; i < ids.Count; i++) if (string.Equals(WithoutCardNumber(ids[i]), loose, StringComparison.Ordinal)) return i;
        return -1;
    }

    /// <summary>The id with ALSA's "(hw:N,M)" suffix removed.</summary>
    public static string WithoutCardNumber(string id) => HwSuffix().Replace(id, string.Empty).TrimEnd();

    [GeneratedRegex(@"\(hw:\d+,\d+\)\s*$")]
    private static partial Regex HwSuffix();
}
