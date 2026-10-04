// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Buffers.Binary;
using System.Text;

namespace Ft8Client.Integrations.Tests;

/// <summary>A minimal QDataStream reader written from the WSJT-X protocol notes, to check our datagrams.</summary>
public sealed class QDataStreamReader(byte[] data)
{
    private int _pos;

    public bool AtEnd => _pos == data.Length;

    public byte UInt8() => data[_pos++];

    public bool Bool() => UInt8() switch
    {
        0 => false,
        1 => true,
        var b => throw new InvalidDataException($"bool byte {b}"),
    };

    public uint UInt32()
    {
        var v = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(_pos, 4));
        _pos += 4;
        return v;
    }

    public int Int32() => unchecked((int)UInt32());

    public ulong UInt64()
    {
        var v = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(_pos, 8));
        _pos += 8;
        return v;
    }

    public double Double() => BitConverter.Int64BitsToDouble(unchecked((long)UInt64()));

    public string? Utf8()
    {
        var n = UInt32();
        if (n == uint.MaxValue) return null;
        var s = Encoding.UTF8.GetString(data, _pos, (int)n);
        _pos += (int)n;
        return s;
    }

    public TimeSpan Time() => TimeSpan.FromMilliseconds(UInt32());

    public DateTime DateTime()
    {
        var jd = unchecked((long)UInt64());
        var ms = UInt32();
        var spec = UInt8();
        if (spec != 1) throw new InvalidDataException($"timespec {spec}");
        // 1 January 2000 is Julian day 2451545.
        return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(jd - 2451545).AddMilliseconds(ms);
    }
}
