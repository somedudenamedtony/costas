// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Backends;

/// <summary>Records what would have been played, for tests and simulation. Never makes a sound.</summary>
public sealed class NullAudioOutput(int sampleRate = 48_000) : IAudioOutput
{
    private readonly object _gate = new();

    /// <inheritdoc />
    public int SampleRate { get; } = sampleRate;

    /// <inheritdoc />
    public bool IsPlaying { get; private set; }

    /// <summary>Every buffer played, with when it started (wall clock).</summary>
    public List<(DateTime StartedUtc, float[] Samples)> Played { get; } = [];

    /// <summary>Clock used to stamp buffers.</summary>
    public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    /// <inheritdoc />
    public event EventHandler<string>? Faulted;

    /// <inheritdoc />
    public void Play(float[] samples)
    {
        lock (_gate)
        {
            Played.Add((Now(), samples));
            IsPlaying = true;
        }
    }

    /// <summary>Marks the current buffer finished (tests call this to simulate the end of audio).</summary>
    public void Finish()
    {
        lock (_gate) IsPlaying = false;
    }

    /// <summary>Simulates a device fault during playback.</summary>
    public void RaiseFault(string why)
    {
        lock (_gate) IsPlaying = false;
        Faulted?.Invoke(this, why);
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_gate) IsPlaying = false;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
