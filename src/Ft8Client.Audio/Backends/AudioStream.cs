// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Audio.Dsp;

namespace Ft8Client.Audio.Backends;

/// <summary>
/// An <see cref="IAudioStream"/> over a lock-free ring buffer: one producer thread writes, the audio thread reads.
/// </summary>
public sealed class AudioStream : IAudioStream
{
    private readonly SpscRingBuffer _ring;
    private volatile bool _complete;
    private long _read;

    /// <param name="capacity">Samples the buffer holds.</param>
    public AudioStream(int capacity) => _ring = new SpscRingBuffer(capacity);

    /// <summary>Samples written and not yet played.</summary>
    public int Buffered => _ring.Count;

    /// <summary>Room left for the producer.</summary>
    public int Free => _ring.Capacity - _ring.Count;

    /// <summary>Samples played so far.</summary>
    public long Played => Interlocked.Read(ref _read);

    /// <inheritdoc />
    public bool Finished => _complete && _ring.Count == 0;

    /// <summary>Adds samples; returns how many fitted.</summary>
    public int Write(ReadOnlySpan<float> samples) => _ring.Write(samples);

    /// <summary>No more samples will be written; the stream finishes once what is buffered has played.</summary>
    public void Complete() => _complete = true;

    /// <inheritdoc />
    public int Read(Span<float> destination)
    {
        var n = _ring.Read(destination);
        Interlocked.Add(ref _read, n);
        return n;
    }
}
