// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Numerics;

namespace Ft8Client.Audio.Dsp;

/// <summary>Windowed receive spectrum at 12 kHz, computed on the capture pump, never the audio callback.</summary>
public sealed class ReceiveSpectrum
{
    /// <summary>FFT length; frequency resolution is approximately 5.9 Hz.</summary>
    public const int Size = 2048;
    private readonly float[] _samples = new float[Size];
    private readonly Complex[] _fft = new Complex[Size];
    private int _count;
    private float[]? _latest;

    /// <summary>Takes the newest complete spectrum, in dBFS, or null when no new audio arrived.</summary>
    public float[]? TakeLatest() => Interlocked.Exchange(ref _latest, null);

    /// <summary>Feeds resampled audio from the capture pump.</summary>
    public void Append(ReadOnlySpan<float> samples)
    {
        foreach (var sample in samples)
        {
            _samples[_count++] = sample;
            if (_count != Size) continue;
            _count = 0;
            for (var i = 0; i < Size; i++)
                _fft[i] = new Complex(_samples[i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Size - 1))), 0);
            for (int i = 1, j = 0; i < Size; i++)
            {
                var bit = Size >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) (_fft[i], _fft[j]) = (_fft[j], _fft[i]);
            }
            for (var length = 2; length <= Size; length <<= 1)
            {
                var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
                for (var start = 0; start < Size; start += length)
                {
                    var phase = Complex.One;
                    for (var k = 0; k < length / 2; k++)
                    {
                        var a = _fft[start + k];
                        var b = phase * _fft[start + k + length / 2];
                        _fft[start + k] = a + b;
                        _fft[start + k + length / 2] = a - b;
                        phase *= step;
                    }
                }
            }
            var row = new float[Size / 4 + 1]; // 0–3000 Hz
            for (var i = 0; i < row.Length; i++)
                row[i] = (float)(20 * Math.Log10(Math.Max(1e-6, _fft[i].Magnitude * 4 / Size)));
            Interlocked.Exchange(ref _latest, row);
        }
    }
}
