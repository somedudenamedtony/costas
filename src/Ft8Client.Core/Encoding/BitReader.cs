// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>Reads fields MSB first from a payload.</summary>
internal ref struct BitReader(ReadOnlySpan<byte> payload)
{
    private readonly ReadOnlySpan<byte> _p = payload;
    private int _pos;

    /// <summary>Reads the next <paramref name="bits"/> bits.</summary>
    public long Get(int bits)
    {
        long v = 0;
        for (var i = 0; i < bits; i++, _pos++) v = (v << 1) | (long)((_p[_pos / 8] >> (7 - _pos % 8)) & 1);
        return v;
    }
}
