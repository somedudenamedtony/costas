// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Numerics;
using System.Text;
using Ft8Client.Modems.Dsp;

namespace Ft8Client.Modems.Rtty;

/// <summary>
/// Reads one RTTY signal (docs/09-keyboard-modes.md section 5). Each tone is mixed to baseband, low-pass filtered,
/// decimated to about 16 samples a bit and integrated over one bit (the matched filter for keyed tones). The bit
/// decision compares the two envelopes with automatic threshold correction (W7AY's ATC), so a signal that loses one
/// tone to selective fading still reads. Characters are framed by their start and stop bits and decoded as ITA2,
/// letters or figures, with unshift-on-space.
/// </summary>
public sealed class RttyDemodulator : IKeyboardReceiver
{
    private const int SamplesPerBit = 16;
    // Characters are held this many bits before they are shown, and dropped if quality falls to DropLevel meanwhile,
    // so the burst of noise read as the squelch notices that a signal has ended is not shown. A clean signal (quality
    // at HighQuality or more) shows its characters at once.
    private const int HoldBits = 22;
    private const double DropLevel = 0.3;
    private const int QualityBits = 8;
    // ATC: each tone's peak decays over about 8 bits, so it follows a fade; its floor rises over about 64 bits, slowly
    // enough that the steady mark at the start of an over does not pull the mark floor up to the peak.
    private const double PeakBits = 8;
    private const double FloorBits = 64;
    private const double HighQuality = 0.8;

    private readonly RttyFormat _format;
    private readonly int _sampleRate;
    private readonly int _decimation;
    private readonly double _bitLength;
    private readonly double _rate;
    private readonly double[] _lowPass;
    private readonly Complex[] _mark;
    private readonly Complex[] _space;
    private readonly Complex[] _markBox;
    private readonly Complex[] _spaceBox;
    private readonly StringBuilder _out = new();
    private readonly Queue<(char Char, long Bit)> _held = new();
    private int _pos;
    private int _count;
    private int _boxPos;
    private Complex _markSum;
    private Complex _spaceSum;
    private Complex _previousMark;
    private Complex _previousSpace;
    private double _markPhase;
    private double _spacePhase;
    private double _markMax, _markMin, _spaceMax, _spaceMin;
    private bool _lastBit = true;
    private bool _inCharacter;
    private double _position;
    private int _idle;
    private readonly double[] _bitSums = new double[7];
    private bool _figures;
    private long _samplesOut;

    /// <param name="format">Speed, shift and polarity.</param>
    /// <param name="sampleRate">Input sample rate.</param>
    /// <param name="centreHz">Audio frequency midway between the tones.</param>
    public RttyDemodulator(RttyFormat format, int sampleRate, double centreHz)
    {
        _format = format;
        _sampleRate = sampleRate;
        StartFrequencyHz = centreHz;
        FrequencyHz = centreHz;
        _decimation = Math.Max(1, (int)Math.Round(sampleRate / (format.Baud * SamplesPerBit)));
        _rate = (double)sampleRate / _decimation;
        _bitLength = _rate / format.Baud;
        var bitSamples = (int)(sampleRate / format.Baud);
        _lowPass = Fir.LowPass(2 * bitSamples | 1, 1.5 * format.Baud / sampleRate);
        _mark = new Complex[_lowPass.Length];
        _space = new Complex[_lowPass.Length];
        _markBox = new Complex[(int)Math.Round(_bitLength)];
        _spaceBox = new Complex[_markBox.Length];
    }

    /// <inheritdoc />
    public double StartFrequencyHz { get; }

    /// <inheritdoc />
    public double FrequencyHz { get; private set; }

    /// <summary>The speed, shift and polarity it reads.</summary>
    public RttyFormat Format => _format;

    /// <summary>Follow drift (on by default).</summary>
    public bool Afc { get; set; } = true;

    /// <summary>How far AFC may move from <see cref="StartFrequencyHz"/>, in hertz.</summary>
    public double AfcRangeHz { get; init; } = 30;

    /// <summary>Return to letters after a space (on by default, as most stations send).</summary>
    public bool UnshiftOnSpace { get; set; } = true;

    /// <inheritdoc />
    public double Quality { get; private set; }

    /// <summary>Quality at or above which received characters are passed on.</summary>
    public double SquelchLevel { get; set; } = 0.45;

    /// <summary>True while characters are being passed on.</summary>
    public bool SquelchOpen => Quality >= SquelchLevel;

    /// <inheritdoc />
    public string Process(ReadOnlySpan<float> samples)
    {
        var markStep = Math.Tau * _format.MarkHz(FrequencyHz) / _sampleRate;
        var spaceStep = Math.Tau * _format.SpaceHz(FrequencyHz) / _sampleRate;
        foreach (var s in samples)
        {
            _mark[_pos] = new Complex(s * Math.Cos(_markPhase), -s * Math.Sin(_markPhase));
            _space[_pos] = new Complex(s * Math.Cos(_spacePhase), -s * Math.Sin(_spacePhase));
            _pos = (_pos + 1) % _mark.Length;
            _markPhase += markStep;
            if (_markPhase > Math.Tau) _markPhase -= Math.Tau;
            _spacePhase += spaceStep;
            if (_spacePhase > Math.Tau) _spacePhase -= Math.Tau;
            if (++_count < _decimation) continue;
            _count = 0;
            Decimated(Filter(_mark), Filter(_space));
            markStep = Math.Tau * _format.MarkHz(FrequencyHz) / _sampleRate;
            spaceStep = Math.Tau * _format.SpaceHz(FrequencyHz) / _sampleRate;
        }
        if (_out.Length == 0) return string.Empty;
        var text = _out.ToString();
        _out.Clear();
        return text;
    }

    private Complex Filter(Complex[] history)
    {
        double re = 0, im = 0;
        var n = history.Length;
        for (var i = 0; i < n; i++)
        {
            var h = history[(_pos + i) % n];
            re += h.Real * _lowPass[i];
            im += h.Imaginary * _lowPass[i];
        }
        return new Complex(re, im);
    }

    private void Decimated(Complex mark, Complex space)
    {
        // Integrate over one bit.
        _markSum += mark - _markBox[_boxPos];
        _spaceSum += space - _spaceBox[_boxPos];
        _markBox[_boxPos] = mark;
        _spaceBox[_boxPos] = space;
        _boxPos = (_boxPos + 1) % _markBox.Length;
        _samplesOut++;
        var zm = _markSum / _markBox.Length;
        var zs = _spaceSum / _spaceBox.Length;
        var m = zm.Magnitude;
        var s = zs.Magnitude;

        // ATC (W7AY's "optimal" form, as in fldigi): each tone's envelope is tracked between its own floor and peak, and
        // each tone's vote is weighted by its own range, so a tone lost in a fade (small range) adds little but noise.
        var fast = 1 / (_bitLength / 4);
        var slow = 1 / (PeakBits * _bitLength);
        var floor = 1 / (FloorBits * _bitLength);
        Track(m, ref _markMax, ref _markMin, fast, slow, floor);
        Track(s, ref _spaceMax, ref _spaceMin, fast, slow, floor);
        var mc = Math.Clamp(m, _markMin, _markMax);
        var sc = Math.Clamp(s, _spaceMin, _spaceMax);
        var a = _markMax - _markMin;
        var b = _spaceMax - _spaceMin;
        var d = (mc - _markMin) * a - (sc - _spaceMin) * b - 0.5 * (a * a - b * b);

        var contrast = m + s > 0 ? Math.Abs(m - s) / (m + s) : 0;
        var bit = d > 0;
        if (Afc && Quality >= HighQuality) Steer(bit ? zm : zs, bit ? _previousMark : _previousSpace);
        _previousMark = zm;
        _previousSpace = zs;

        Frame(bit, d, contrast);
        _lastBit = bit;
        if (Quality < DropLevel) _held.Clear();
        var now = (long)(_samplesOut / _bitLength);
        while (_held.Count > 0 && (now - _held.Peek().Bit >= HoldBits || Quality >= HighQuality)) Emit(_held.Dequeue().Char);
    }

    private static void Track(double v, ref double max, ref double min, double fast, double slow, double floor)
    {
        max += (v - max) * (v > max ? fast : slow);
        min += (v - min) * (v < min ? fast : floor);
    }

    // AFC: the active tone's phase turns at the frequency error; a slow loop takes it out, within the AFC range.
    private void Steer(Complex z, Complex previous)
    {
        if (z == Complex.Zero || previous == Complex.Zero) return;
        var hz = (z * Complex.Conjugate(previous)).Phase * _rate / Math.Tau;
        FrequencyHz = Math.Clamp(FrequencyHz + hz / 64, StartFrequencyHz - AfcRangeHz, StartFrequencyHz + AfcRangeHz);
    }

    // Start-bit framing: a mark-to-space edge starts a character; each bit is decided on the middle half of its time,
    // and a character whose start bit is not space or whose stop bit is not mark is noise and is dropped.
    // Quality is the contrast between the tones, |m - s| / (m + s), taken once a bit at the middle of the bit and
    // averaged over about eight bits: near 1 for a keyed signal, low for noise. (Taken anywhere else it would count the
    // moments where the tones cross, when even a clean signal has no contrast.)
    private void Frame(bool bit, double d, double contrast)
    {
        if (!_inCharacter)
        {
            if (++_idle >= _bitLength)
            {
                _idle = 0;
                Quality += (contrast - Quality) / QualityBits;
            }
            if (_lastBit && !bit)
            {
                _inCharacter = true;
                _position = 0;
                _idle = 0;
                Array.Clear(_bitSums);
            }
            return;
        }
        _position += 1;
        var index = (int)(_position / _bitLength);
        var within = _position / _bitLength - index;
        if (index < 7 && within >= 0.5 && within - 1 / _bitLength < 0.5) Quality += (contrast - Quality) / QualityBits;
        if (index < 7 && within is >= 0.25 and <= 0.75) _bitSums[index] += d;
        if (_position < 6.75 * _bitLength) return;
        _inCharacter = false;
        if (_bitSums[0] >= 0 || _bitSums[6] <= 0) return;
        var code = 0;
        for (var b = 0; b < 5; b++) if (_bitSums[b + 1] > 0) code |= 1 << b;
        Character(code);
    }

    private void Character(int code)
    {
        switch (code)
        {
            case Ita2.Letters:
                _figures = false;
                return;
            case Ita2.Figures:
                _figures = true;
                return;
            case Ita2.Space:
                if (UnshiftOnSpace) _figures = false;
                break;
        }
        if (!SquelchOpen || !Ita2.TryDecode(code, _figures, out var c) || c is '\r' or '\a') return;
        _held.Enqueue((c, (long)(_samplesOut / _bitLength)));
    }

    private void Emit(char c) => _out.Append(c);
}
