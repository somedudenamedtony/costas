// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Numerics;
using Ft8Client.Core;
using Ft8Client.Modems.Dsp;

namespace Ft8Client.Modems.Psk;

/// <summary>
/// Finds PSK signals in the passband (docs/09-keyboard-modes.md section 5). The power spectrum (4,096-point FFT,
/// about 3 Hz per bin at 12 kHz) is averaged over the last few seconds and smoothed over one signal's width; a peak
/// standing <see cref="ThresholdDb"/> above the median of the band is a signal, and its carrier is the power-weighted
/// centre of the peak.
/// </summary>
public sealed class PskChannelFinder
{
    private const int Size = 4096;

    private readonly int _sampleRate;
    private readonly double _widthHz;
    private readonly float[] _buffer = new float[Size];
    private int _filled;
    private readonly double[] _average = new double[Size / 2];
    private readonly double[] _window;
    private int _frames;

    /// <param name="mode">PSK31 or PSK63: sets the expected signal width.</param>
    /// <param name="sampleRate">Input sample rate.</param>
    public PskChannelFinder(Mode mode, int sampleRate)
    {
        _sampleRate = sampleRate;
        _widthHz = 2 * PskMode.Baud(mode);
        _window = Enumerable.Range(0, Size).Select(i => 0.5 - 0.5 * Math.Cos(Math.Tau * i / (Size - 1))).ToArray();
    }

    /// <summary>Lowest audio frequency searched.</summary>
    public double LowHz { get; init; } = 200;

    /// <summary>Highest audio frequency searched.</summary>
    public double HighHz { get; init; } = 2900;

    /// <summary>How far above the band's median a signal must stand, in dB.</summary>
    public double ThresholdDb { get; init; } = 6;

    /// <summary>Feeds audio. Every 4,096 samples (half overlapped) the averaged spectrum is updated.</summary>
    public void Process(ReadOnlySpan<float> samples)
    {
        foreach (var s in samples)
        {
            _buffer[_filled++] = s;
            if (_filled < Size) continue;
            Frame();
            Array.Copy(_buffer, Size / 2, _buffer, 0, Size / 2);
            _filled = Size / 2;
        }
    }

    /// <summary>The carrier frequencies of the signals found, lowest first.</summary>
    public IReadOnlyList<double> Signals()
    {
        if (_frames == 0) return [];
        var binHz = (double)_sampleRate / Size;
        var lo = (int)(LowHz / binHz);
        var hi = Math.Min((int)(HighHz / binHz), _average.Length - 1);
        var half = Math.Max(1, (int)(_widthHz / 2 / binHz));

        var smooth = new double[_average.Length];
        for (var i = lo; i <= hi; i++)
        {
            double sum = 0;
            for (var k = -half; k <= half; k++) sum += _average[Math.Clamp(i + k, 0, _average.Length - 1)];
            smooth[i] = sum / (2 * half + 1);
        }
        var floor = smooth[lo..(hi + 1)].Order().ElementAt((hi - lo + 1) / 2);
        // Digital silence has no floor to stand above.
        if (floor <= 0) return [];
        var limit = floor * Math.Pow(10, ThresholdDb / 10);

        var found = new List<double>();
        for (var i = lo + 1; i < hi; i++)
        {
            if (smooth[i] < limit || smooth[i] < smooth[i - 1] || smooth[i] < smooth[i + 1]) continue;
            double weight = 0, moment = 0;
            for (var k = -2 * half; k <= 2 * half; k++)
            {
                var j = i + k;
                if (j < 0 || j >= _average.Length) continue;
                var p = Math.Max(0, _average[j] - floor);
                weight += p;
                moment += p * j * binHz;
            }
            var centre = weight > 0 ? moment / weight : i * binHz;
            if (found.Count > 0 && centre - found[^1] < _widthHz) continue;
            found.Add(centre);
        }
        return found;
    }

    private void Frame()
    {
        var data = new Complex[Size];
        for (var i = 0; i < Size; i++) data[i] = _buffer[i] * _window[i];
        Fft.Forward(data);
        // An exponential average over about eight frames (1.4 s at 12 kHz) follows signals coming and going.
        var alpha = _frames < 8 ? 1.0 / (_frames + 1) : 1.0 / 8;
        for (var i = 0; i < _average.Length; i++) _average[i] += (data[i].Real * data[i].Real + data[i].Imaginary * data[i].Imaginary - _average[i]) * alpha;
        _frames++;
    }
}
