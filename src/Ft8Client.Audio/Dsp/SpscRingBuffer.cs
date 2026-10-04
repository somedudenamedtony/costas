// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Dsp;

/// <summary>
/// Single-producer single-consumer lock-free ring buffer of floats. The audio callback writes, one worker reads.
/// Neither side allocates or locks. When full, the writer drops the newest samples and counts an overrun.
/// </summary>
public sealed class SpscRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private long _write;
    private long _read;
    private long _overruns;

    /// <summary>Creates a buffer holding at least <paramref name="capacity"/> samples (rounded up to a power of two).</summary>
    public SpscRingBuffer(int capacity)
    {
        var size = 1;
        while (size < capacity) size <<= 1;
        _buffer = new float[size];
        _mask = size - 1;
    }

    /// <summary>Capacity in samples.</summary>
    public int Capacity => _buffer.Length;

    /// <summary>Samples waiting to be read.</summary>
    public int Count => (int)(Volatile.Read(ref _write) - Volatile.Read(ref _read));

    /// <summary>Samples dropped because the reader fell behind.</summary>
    public long Overruns => Interlocked.Read(ref _overruns);

    /// <summary>Writes as many samples as fit. Returns the number written. Producer side only.</summary>
    public int Write(ReadOnlySpan<float> samples)
    {
        var w = _write;
        var free = _buffer.Length - (int)(w - Volatile.Read(ref _read));
        var n = Math.Min(free, samples.Length);
        for (var i = 0; i < n; i++) _buffer[(int)((w + i) & _mask)] = samples[i];
        Volatile.Write(ref _write, w + n);
        if (n < samples.Length) Interlocked.Add(ref _overruns, samples.Length - n);
        return n;
    }

    /// <summary>Reads up to <paramref name="destination"/>.Length samples. Returns the number read. Consumer side only.</summary>
    public int Read(Span<float> destination)
    {
        var r = _read;
        var available = (int)(Volatile.Read(ref _write) - r);
        var n = Math.Min(available, destination.Length);
        for (var i = 0; i < n; i++) destination[i] = _buffer[(int)((r + i) & _mask)];
        Volatile.Write(ref _read, r + n);
        return n;
    }

    /// <summary>Discards everything waiting. Consumer side only.</summary>
    public void Clear() => Volatile.Write(ref _read, Volatile.Read(ref _write));
}
