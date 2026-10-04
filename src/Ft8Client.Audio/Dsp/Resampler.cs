// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Dsp;

/// <summary>
/// Streaming windowed-sinc (Blackman) resampler for any rate ratio. Low-pass at 0.45 of the lower Nyquist.
/// Stateful across calls; allocates only in the constructor and when the input block grows.
/// </summary>
public sealed class Resampler
{
    private const int HalfTaps = 32;
    private const int Phases = 256;
    private readonly double _step;
    private readonly float[] _table;
    private float[] _history;
    private int _historyCount;
    private double _pos;

    /// <summary>Creates a resampler from <paramref name="inRate"/> to <paramref name="outRate"/>.</summary>
    public Resampler(int inRate, int outRate)
    {
        InRate = inRate;
        OutRate = outRate;
        _step = (double)inRate / outRate;
        var cutoff = 0.45 * Math.Min(1.0, (double)outRate / inRate) * 2; // fraction of the input Nyquist
        _table = new float[(Phases + 1) * 2 * HalfTaps];
        for (var p = 0; p <= Phases; p++)
        {
            var frac = (double)p / Phases;
            double sum = 0;
            for (var k = 0; k < 2 * HalfTaps; k++)
            {
                var x = k - HalfTaps + 1 - frac;
                var sinc = Math.Abs(x) < 1e-9 ? 1.0 : Math.Sin(Math.PI * cutoff * x) / (Math.PI * cutoff * x);
                var wpos = (x + HalfTaps) / (2.0 * HalfTaps);
                var w = 0.42 - 0.5 * Math.Cos(2 * Math.PI * wpos) + 0.08 * Math.Cos(4 * Math.PI * wpos);
                var v = cutoff * sinc * Math.Max(0, w);
                _table[p * 2 * HalfTaps + k] = (float)v;
                sum += v;
            }
            for (var k = 0; k < 2 * HalfTaps; k++) _table[p * 2 * HalfTaps + k] /= (float)sum;
        }
        _history = new float[4096 + 2 * HalfTaps];
        _historyCount = 2 * HalfTaps - 1;
        _pos = HalfTaps - 1;
    }

    /// <summary>Input rate.</summary>
    public int InRate { get; }

    /// <summary>Output rate.</summary>
    public int OutRate { get; }

    /// <summary>Upper bound on output samples for an input block.</summary>
    public int MaxOutput(int inputCount) => (int)Math.Ceiling((inputCount + 2) / _step) + 2;

    /// <summary>Resamples a block. Returns the number of samples written to <paramref name="output"/>.</summary>
    public int Process(ReadOnlySpan<float> input, Span<float> output)
    {
        if (InRate == OutRate)
        {
            input.CopyTo(output);
            return input.Length;
        }
        if (_historyCount + input.Length > _history.Length) Array.Resize(ref _history, (_historyCount + input.Length) * 2);
        input.CopyTo(_history.AsSpan(_historyCount));
        _historyCount += input.Length;

        var n = 0;
        while (_pos + HalfTaps < _historyCount && n < output.Length)
        {
            var i = (int)_pos;
            var frac = _pos - i;
            var phase = (int)(frac * Phases);
            var t = phase * 2 * HalfTaps;
            var start = i - HalfTaps + 1;
            float acc = 0;
            for (var k = 0; k < 2 * HalfTaps; k++) acc += _history[start + k] * _table[t + k];
            output[n++] = acc;
            _pos += _step;
        }

        var keepFrom = Math.Max(0, (int)_pos - HalfTaps + 1);
        var keep = _historyCount - keepFrom;
        Array.Copy(_history, keepFrom, _history, 0, keep);
        _historyCount = keep;
        _pos -= keepFrom;
        return n;
    }
}
