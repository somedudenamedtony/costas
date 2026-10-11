// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Dsp;

/// <summary>
/// Classifies the receive audio level for Setup's input meter, and decides when the level has been good long enough
/// to pass the step. Fed from a polling thread, never from the audio callback.
/// </summary>
public sealed class InputLevelMeter
{
    /// <summary>Bottom of the good band, RMS dBFS.</summary>
    public const double GoodLowDbfs = -40;

    /// <summary>Top of the good band, RMS dBFS.</summary>
    public const double GoodHighDbfs = -10;

    /// <summary>A sample peak at or above this counts as clipping, dBFS.</summary>
    public const double ClipDbfs = -1;

    /// <summary>No samples for this long means no audio is arriving.</summary>
    public static readonly TimeSpan NoAudioAfter = TimeSpan.FromSeconds(1);

    /// <summary>The level must stay in the good band this long to pass.</summary>
    public static readonly TimeSpan PassAfter = TimeSpan.FromSeconds(5);

    private DateTime _lastSamplesUtc;
    private DateTime? _goodSinceUtc;

    /// <summary>Starts measuring at <paramref name="startUtc"/>.</summary>
    public InputLevelMeter(DateTime startUtc) => _lastSamplesUtc = startUtc;

    /// <summary>RMS level of the last block, dBFS; -120 for silence.</summary>
    public double RmsDbfs { get; private set; } = -120;

    /// <summary>Peak of the last block, dBFS; -120 for silence.</summary>
    public double PeakDbfs { get; private set; } = -120;

    /// <summary>The current state.</summary>
    public InputLevelState State { get; private set; } = InputLevelState.Waiting;

    /// <summary>The level has stayed good for <see cref="PassAfter"/>. Stays true once reached.</summary>
    public bool Passed { get; private set; }

    /// <summary>Takes the samples read since the last call (possibly none) at time <paramref name="nowUtc"/>.</summary>
    public void Update(ReadOnlySpan<float> samples, DateTime nowUtc)
    {
        if (samples.IsEmpty)
        {
            if (nowUtc - _lastSamplesUtc >= NoAudioAfter) Set(InputLevelState.NoAudio, -120, -120, nowUtc);
            return;
        }
        _lastSamplesUtc = nowUtc;
        float peak = 0;
        foreach (var v in samples) peak = Math.Max(peak, Math.Abs(v));
        var peakDb = peak <= 1e-6f ? -120 : 20 * Math.Log10(peak);
        var rms = AudioLevel.RmsDbfs(samples);
        var state = peak == 0 ? InputLevelState.Silence
            : peakDb >= ClipDbfs ? InputLevelState.Clipping
            : rms < GoodLowDbfs ? InputLevelState.TooQuiet
            : rms > GoodHighDbfs ? InputLevelState.TooLoud
            : InputLevelState.Good;
        Set(state, rms, peakDb, nowUtc);
    }

    private void Set(InputLevelState state, double rms, double peak, DateTime nowUtc)
    {
        State = state;
        RmsDbfs = rms;
        PeakDbfs = peak;
        if (state != InputLevelState.Good)
        {
            _goodSinceUtc = null;
            return;
        }
        _goodSinceUtc ??= nowUtc;
        if (nowUtc - _goodSinceUtc >= PassAfter) Passed = true;
    }
}
