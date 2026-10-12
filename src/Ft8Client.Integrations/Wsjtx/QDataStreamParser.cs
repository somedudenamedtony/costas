// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Buffers.Binary;
using System.Text;

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>
/// Reads values the way Qt's <c>QDataStream</c> writes them (big-endian), for datagrams other programs send us. Throws
/// <see cref="InvalidDataException"/> when the data runs out or a value is malformed.
/// </summary>
public sealed class QDataStreamParser(byte[] data)
{
    private int _pos;

    /// <summary>Reads a <c>quint8</c>.</summary>
    public byte UInt8()
    {
        Need(1);
        return data[_pos++];
    }

    /// <summary>Reads a <c>bool</c> (one byte; any non-zero value is true, as Qt reads it).</summary>
    public bool Bool() => UInt8() != 0;

    /// <summary>Reads a <c>quint16</c>.</summary>
    public ushort UInt16()
    {
        Need(2);
        var v = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(_pos, 2));
        _pos += 2;
        return v;
    }

    /// <summary>Reads a <c>quint32</c>.</summary>
    public uint UInt32()
    {
        Need(4);
        var v = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(_pos, 4));
        _pos += 4;
        return v;
    }

    /// <summary>Reads a <c>qint32</c>.</summary>
    public int Int32() => unchecked((int)UInt32());

    /// <summary>Reads a <c>double</c>.</summary>
    public double Double()
    {
        Need(8);
        var v = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(_pos, 8));
        _pos += 8;
        return BitConverter.Int64BitsToDouble(v);
    }

    /// <summary>Reads a <c>utf8</c> string: byte length then bytes; length <c>0xffffffff</c> is null.</summary>
    public string? Utf8()
    {
        var n = UInt32();
        if (n == uint.MaxValue) return null;
        if (n > data.Length - _pos) throw new InvalidDataException("String longer than the datagram.");
        var s = Encoding.UTF8.GetString(data, _pos, (int)n);
        _pos += (int)n;
        return s;
    }

    /// <summary>Reads a <c>QTime</c> (milliseconds since midnight) as a time of day.</summary>
    public TimeSpan Time() => TimeSpan.FromMilliseconds(UInt32());

    /// <summary>
    /// Reads a <c>QColor</c>: <c>qint8</c> spec, then alpha, red, green, blue and a pad as <c>quint16</c>. Returns null for
    /// an invalid colour (spec 0), which the protocol uses to cancel a highlight. Only RGB colours are taken as given;
    /// other specs are read past and returned as null.
    /// </summary>
    public WsjtxColor? Color()
    {
        var spec = UInt8();
        var a = UInt16();
        var r = UInt16();
        var g = UInt16();
        var b = UInt16();
        UInt16();
        return spec == 1 ? new WsjtxColor((byte)(a >> 8), (byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8)) : null;
    }

    private void Need(int n)
    {
        if (data.Length - _pos < n) throw new InvalidDataException("Datagram ended early.");
    }
}
