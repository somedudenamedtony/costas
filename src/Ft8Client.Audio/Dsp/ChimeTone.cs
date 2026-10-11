// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Dsp;

/// <summary>A short two-note chime for the computer's speakers (never the radio's audio device).</summary>
public static class ChimeTone
{
    /// <summary>Renders the chime as mono samples at <paramref name="sampleRate"/>.</summary>
    public static float[] Render(int sampleRate)
    {
        const double noteSeconds = 0.14;
        const float level = 0.25f;
        double[] notes = [880, 1320];
        var perNote = (int)(sampleRate * noteSeconds);
        var fade = sampleRate / 200; // 5 ms ramps, so the notes do not click
        var samples = new float[perNote * notes.Length];
        for (var n = 0; n < notes.Length; n++)
        {
            for (var i = 0; i < perNote; i++)
            {
                var ramp = Math.Min(1.0, Math.Min(i, perNote - 1 - i) / (double)fade);
                samples[n * perNote + i] = (float)(level * ramp * Math.Sin(2 * Math.PI * notes[n] * i / sampleRate));
            }
        }
        return samples;
    }
}
