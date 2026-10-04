// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Backends;

/// <summary>An open playback device. Plays one pre-rendered buffer at a time.</summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Sample rate the output expects.</summary>
    int SampleRate { get; }

    /// <summary>True while samples are playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Raised when playback stops for any reason other than <see cref="Stop"/>; the text says why.</summary>
    event EventHandler<string>? Faulted;

    /// <summary>Starts playing mono samples now. The buffer is not copied and must not change while playing.</summary>
    void Play(float[] samples);

    /// <summary>Stops playback immediately.</summary>
    void Stop();
}
