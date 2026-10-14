// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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

    /// <summary>The stream being played, if any.</summary>
    public IAudioStream? Stream { get; private set; }

    /// <summary>Every sample read from streams so far (tests read them with <see cref="Pull"/>).</summary>
    public List<float> Streamed { get; } = [];

    /// <summary>
    /// Reads streams at the real sample rate on a thread of its own, as a device would, without keeping the samples
    /// (simulation and a muted radio). Off for tests, which move streams on with <see cref="Pull"/>.
    /// </summary>
    public bool RealTime { get; init; }

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

    /// <inheritdoc />
    public void PlayStream(IAudioStream stream)
    {
        lock (_gate)
        {
            Stream = stream;
            IsPlaying = true;
        }
        if (RealTime) new Thread(() => Drain(stream)) { IsBackground = true, Name = "Null audio output" }.Start();
    }

    // Plays a stream in real time, 10 ms at a time, until it finishes or another replaces it.
    private void Drain(IAudioStream stream)
    {
        var buffer = new float[SampleRate / 100];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long played = 0;
        while (true)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(Stream, stream)) return;
                stream.Read(buffer);
                played += buffer.Length;
                if (stream.Finished)
                {
                    Stream = null;
                    IsPlaying = false;
                    return;
                }
            }
            var ahead = played / (double)SampleRate - clock.Elapsed.TotalSeconds;
            if (ahead > 0) Thread.Sleep(TimeSpan.FromSeconds(ahead));
        }
    }

    /// <summary>
    /// Plays <paramref name="samples"/> of the stream as a device would (tests call it to move time on). Returns how many
    /// the stream had ready.
    /// </summary>
    public int Pull(int samples)
    {
        IAudioStream? s;
        lock (_gate) s = Stream;
        if (s is null) return 0;
        var buffer = new float[samples];
        var n = s.Read(buffer);
        lock (_gate)
        {
            Streamed.AddRange(buffer.AsSpan(0, n).ToArray());
            if (s.Finished && ReferenceEquals(Stream, s))
            {
                Stream = null;
                IsPlaying = false;
            }
        }
        return n;
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
        lock (_gate)
        {
            IsPlaying = false;
            Stream = null;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
