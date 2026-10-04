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

/// <summary>Writes values the way Qt's <c>QDataStream</c> does (big-endian), for the WSJT-X UDP protocol.</summary>
public sealed class QDataStreamWriter
{
    private readonly MemoryStream _buffer = new();

    /// <summary>Writes a <c>quint8</c>.</summary>
    public QDataStreamWriter UInt8(byte v)
    {
        _buffer.WriteByte(v);
        return this;
    }

    /// <summary>Writes a <c>bool</c> (one byte).</summary>
    public QDataStreamWriter Bool(bool v) => UInt8(v ? (byte)1 : (byte)0);

    /// <summary>Writes a <c>quint32</c>.</summary>
    public QDataStreamWriter UInt32(uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, v);
        _buffer.Write(b);
        return this;
    }

    /// <summary>Writes a <c>qint32</c>.</summary>
    public QDataStreamWriter Int32(int v) => UInt32(unchecked((uint)v));

    /// <summary>Writes a <c>quint64</c>.</summary>
    public QDataStreamWriter UInt64(ulong v)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(b, v);
        _buffer.Write(b);
        return this;
    }

    /// <summary>Writes a <c>qint64</c>.</summary>
    public QDataStreamWriter Int64(long v) => UInt64(unchecked((ulong)v));

    /// <summary>Writes a <c>double</c>.</summary>
    public QDataStreamWriter Double(double v) => UInt64(unchecked((ulong)BitConverter.DoubleToInt64Bits(v)));

    /// <summary>Writes a <c>utf8</c> string: byte length then bytes; null is length <c>0xffffffff</c>.</summary>
    public QDataStreamWriter Utf8(string? v)
    {
        if (v is null) return UInt32(uint.MaxValue);
        var bytes = Encoding.UTF8.GetBytes(v);
        UInt32((uint)bytes.Length);
        _buffer.Write(bytes);
        return this;
    }

    /// <summary>Writes a <c>QTime</c> as milliseconds since midnight.</summary>
    public QDataStreamWriter Time(DateTime utc) => UInt32((uint)(utc.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond));

    /// <summary>Writes a UTC <c>QDateTime</c>: Julian day (qint64), milliseconds since midnight (quint32), timespec 1 (UTC).</summary>
    public QDataStreamWriter DateTimeUtc(DateTime utc)
    {
        Int64(JulianDay(utc));
        Time(utc);
        return UInt8(1);
    }

    /// <summary>The Julian day number of a date (2 January 4713 BC is day 0 in Qt's numbering; 1 January 2000 is 2451545).</summary>
    public static long JulianDay(DateTime d) => d.Date.Ticks / TimeSpan.TicksPerDay + 1_721_426;

    /// <summary>The bytes written so far.</summary>
    public byte[] ToArray() => _buffer.ToArray();
}
