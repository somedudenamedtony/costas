// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;

namespace Ft8Client.Audio.Backends;

/// <summary>An open capture device delivering mono float samples into a ring buffer.</summary>
public interface IAudioInput : IDisposable
{
    /// <summary>Sample rate of the samples in <see cref="Buffer"/>.</summary>
    int SampleRate { get; }

    /// <summary>Mono samples written by the audio callback.</summary>
    SpscRingBuffer Buffer { get; }

    /// <summary>Raised (off the audio thread) when the device stops or is removed.</summary>
    event EventHandler<string>? Stopped;

    /// <summary>Starts capturing.</summary>
    void Start();
}
