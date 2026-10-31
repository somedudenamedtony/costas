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
using Ft8Client.Modems.Psk;

namespace Ft8Client.Modems;

/// <summary>
/// Finds narrow signals in the passband (docs/09-keyboard-modes.md section 5): PSK carriers, or the tones of an RTTY
/// signal for <see cref="Rtty.RttyChannelFinder"/>. The power spectrum (4,096-point FFT, about 3 Hz per bin at 12 kHz)
/// is averaged over the last few seconds and smoothed over half a signal's width; a peak standing
/// <see cref="ThresholdDb"/> above the noise floor (a low percentile of the band, so a crowded band still has one) is
/// a signal, and its frequency is the power-weighted centre of the peaks within 0.6 of a signal's width of it.
/// </summary>
public sealed class ChannelFinder : IChannelFinder
{
    private const int Size = 4096;

    private readonly int _sampleRate;
    private readonly double _widthHz;
    private readonly float[] _buffer = new float[Size];
    private int _filled;
    private readonly double[] _average = new double[Size / 2];
    private readonly double[] _window;
    private int _frames;

    /// <summary>A finder for PSK31 or PSK63 carriers.</summary>
    public ChannelFinder(Mode mode, int sampleRate)
        : this(sampleRate, 2 * PskMode.Baud(mode))
    {
    }

    /// <param name="sampleRate">Input sample rate.</param>
    /// <param name="widthHz">The width of one signal (or one tone).</param>
    public ChannelFinder(int sampleRate, double widthHz)
    {
        _sampleRate = sampleRate;
        _widthHz = widthHz;
        _window = Enumerable.Range(0, Size).Select(i => 0.5 - 0.5 * Math.Cos(Math.Tau * i / (Size - 1))).ToArray();
    }

    /// <summary>Lowest audio frequency searched.</summary>
    public double LowHz { get; init; } = 200;

    /// <summary>Highest audio frequency searched.</summary>
    public double HighHz { get; init; } = 2900;

    /// <summary>How far above the band's noise floor a signal must stand, in dB.</summary>
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

    /// <summary>The centre frequencies of the peaks found, lowest first.</summary>
    public IReadOnlyList<double> Signals()
    {
        if (_frames == 0) return [];
        var binHz = (double)_sampleRate / Size;
        var lo = (int)(LowHz / binHz);
        var hi = Math.Min((int)(HighHz / binHz), _average.Length - 1);
        // Smoothed over half a signal's width, so two signals a little more than half a width apart stay two peaks.
        var half = Math.Max(1, (int)(_widthHz / 4 / binHz));

        var smooth = new double[_average.Length];
        for (var i = lo; i <= hi; i++)
        {
            double sum = 0;
            for (var k = -half; k <= half; k++) sum += _average[Math.Clamp(i + k, 0, _average.Length - 1)];
            smooth[i] = sum / (2 * half + 1);
        }
        var floor = Floor(smooth, lo, hi);
        // Digital silence has no floor to stand above.
        if (floor <= 0) return [];
        var limit = floor * Math.Pow(10, ThresholdDb / 10);

        // Peaks closer than Spacing are one signal (PSK idle reversals are two lines one baud apart): each group's
        // centre is the power-weighted centre over the group and a little either side.
        var spacing = Spacing * _widthHz;
        var reach = (int)Math.Ceiling(_widthHz * 0.4 / binHz);
        var found = new List<double>();
        int first = -1, last = -1;
        for (var i = lo + 1; i < hi; i++)
        {
            if (smooth[i] < limit || smooth[i] < smooth[i - 1] || smooth[i] < smooth[i + 1]) continue;
            if (first >= 0 && (i - last) * binHz < spacing)
            {
                last = i;
                continue;
            }
            if (first >= 0) found.Add(Centre(first - reach, last + reach, floor, binHz));
            first = last = i;
        }
        if (first >= 0) found.Add(Centre(first - reach, last + reach, floor, binHz));
        return found;
    }

    /// <summary>Peaks closer than this share of a signal's width are taken as one signal.</summary>
    private const double Spacing = 0.6;

    /// <summary>
    /// The share of the band taken as noise: its power is this low percentile of the smoothed spectrum, not the median,
    /// so a crowded band (more than half of it under signals) still has a floor.
    /// </summary>
    private const double FloorPercentile = 0.2;

    private static double Floor(double[] spectrum, int lo, int hi) =>
        spectrum[lo..(hi + 1)].Order().ElementAt((int)((hi - lo + 1) * FloorPercentile));

    private double Centre(int from, int to, double floor, double binHz)
    {
        double weight = 0, moment = 0;
        for (var j = Math.Max(0, from); j <= Math.Min(_average.Length - 1, to); j++)
        {
            var p = Math.Max(0, _average[j] - floor);
            weight += p;
            moment += p * j * binHz;
        }
        return weight > 0 ? moment / weight : (from + to) / 2.0 * binHz;
    }

    /// <summary>
    /// The averaged power spectrum smoothed over <paramref name="smoothHz"/>, with its bin width and noise floor (see
    /// <see cref="FloorPercentile"/>); null before any spectrum or in digital silence.
    /// </summary>
    internal (double[] Power, double BinHz, double Floor, int Lo, int Hi)? Smoothed(double smoothHz)
    {
        if (_frames == 0) return null;
        var binHz = (double)_sampleRate / Size;
        var lo = (int)(LowHz / binHz);
        var hi = Math.Min((int)(HighHz / binHz), _average.Length - 1);
        var half = Math.Max(0, (int)(smoothHz / 2 / binHz));
        var smooth = new double[_average.Length];
        for (var i = 0; i < smooth.Length; i++)
        {
            double sum = 0;
            for (var k = -half; k <= half; k++) sum += _average[Math.Clamp(i + k, 0, _average.Length - 1)];
            smooth[i] = sum / (2 * half + 1);
        }
        var floor = Floor(smooth, lo, hi);
        return floor <= 0 ? null : (smooth, binHz, floor, lo, hi);
    }

    /// <summary>
    /// The SNR of a signal at <paramref name="hz"/> in dB in 2,500 Hz, as WSJT-X measures FT8, so the dB column means the
    /// same in every mode: the signal's power over its width above the band's median, against the median noise over
    /// 2,500 Hz. Null before any spectrum, or when nothing stands above the noise there.
    /// </summary>
    public int? SnrDb(double hz) => SnrDb([hz]);

    /// <summary>The SNR of a signal made of narrow parts at these frequencies (an RTTY signal's two tones), as above.</summary>
    public int? SnrDb(ReadOnlySpan<double> parts)
    {
        if (_frames == 0) return null;
        var binHz = (double)_sampleRate / Size;
        var lo = (int)(LowHz / binHz);
        var hi = Math.Min((int)(HighHz / binHz), _average.Length - 1);
        var floor = _average[lo..(hi + 1)].Order().ElementAt((hi - lo + 1) / 2);
        if (floor <= 0) return null;
        double power = 0;
        var half = (int)Math.Ceiling(_widthHz / binHz);
        foreach (var hz in parts)
        {
            var centre = (int)Math.Round(hz / binHz);
            for (var i = Math.Max(0, centre - half); i <= Math.Min(_average.Length - 1, centre + half); i++) power += _average[i] - floor;
        }
        // The median of an exponential (noise power) distribution is ln 2 of its mean.
        var noise = floor / Math.Log(2) * 2500 / binHz;
        return power <= 0 ? null : (int)Math.Round(10 * Math.Log10(power / noise));
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
