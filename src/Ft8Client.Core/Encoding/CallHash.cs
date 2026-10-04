// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>Callsign hashes (22, 12 and 10 bits) used for nonstandard calls. From ft8_lib save_callsign (MIT, Kārlis Goba).</summary>
public static class CallHash
{
    /// <summary>The 22-bit hash of a call (up to 11 characters of A-Z, 0-9, space and slash). Null if the call has other characters.</summary>
    public static uint? Hash22(string call)
    {
        ulong n58 = 0;
        var i = 0;
        for (; i < call.Length && i < 11; i++)
        {
            var j = CharTables.Index(call[i], CharTables.AlnumSpaceSlash);
            if (j < 0) return null;
            n58 = 38 * n58 + (ulong)j;
        }
        for (; i < 11; i++) n58 *= 38;
        return (uint)((47055833459UL * n58) >> (64 - 22)) & 0x3FFFFF;
    }

    /// <summary>The 12-bit hash.</summary>
    public static uint? Hash12(string call) => Hash22(call) >> 10;

    /// <summary>The 10-bit hash.</summary>
    public static uint? Hash10(string call) => Hash22(call) >> 12;
}
