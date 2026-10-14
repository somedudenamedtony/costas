// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Numerics;
using System.Text;
using Ft8Client.Core;
using Ft8Client.Modems.Dsp;

namespace Ft8Client.Modems.Psk;

/// <summary>
/// Receives one BPSK31/63 signal at a given audio frequency (docs/09-keyboard-modes.md section 5).
/// <list type="number">
/// <item>Mix to baseband and low-pass while decimating to 16 samples per symbol.</item>
/// <item>A raised-cosine matched filter.</item>
/// <item>Symbol timing: the 16 sample phases keep a running magnitude, and the clock is nudged until the peak sits where
/// the clock wraps; the symbol is taken there.</item>
/// <item>Each symbol is compared with the one before: a phase reversal is a 0 bit, no change a 1 bit.</item>
/// <item>Varicode: "00" ends a character.</item>
/// </list>
/// AFC follows the residual phase rotation per symbol. Squelch is the running average of cos(2Δφ): near 1 for a clean
/// signal, near 0 for noise; characters are passed on only while it is above <see cref="SquelchLevel"/>.
/// Not thread-safe; feed it from one thread.
/// </summary>
public sealed class PskDemodulator : IKeyboardReceiver
{
    private const int SamplesPerSymbolDecimated = 16;

    private readonly int _sampleRate;
    private readonly int _decimation;
    private readonly double _baud;
    private readonly double[] _lowPass;
    private readonly Complex[] _history;
    private int _historyPos;
    private int _decimationCount;
    private readonly double[] _matched;
    private readonly Complex[] _matchedHistory;
    private int _matchedPos;
    private readonly double[] _sync = new double[SamplesPerSymbolDecimated];
    private double _bitClock;
    private Complex _previous;
    private double _ncoPhase;
    private int _code;
    private int _zeros;
    private readonly StringBuilder _out = new();

    /// <param name="mode">PSK31 or PSK63.</param>
    /// <param name="sampleRate">Input sample rate (12 kHz in Costas).</param>
    /// <param name="frequencyHz">Audio frequency of the signal's carrier.</param>
    public PskDemodulator(Mode mode, int sampleRate, double frequencyHz)
    {
        _sampleRate = sampleRate;
        _baud = PskMode.Baud(mode);
        var symbol = PskMode.SymbolSamples(mode, sampleRate);
        if (symbol % SamplesPerSymbolDecimated != 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "A symbol must split into 16 equal parts.");
        _decimation = symbol / SamplesPerSymbolDecimated;
        // Stage one passes about three times the baud rate and stops well before the decimated Nyquist (8 × baud).
        _lowPass = Fir.LowPass(8 * symbol / 3 | 1, 3 * _baud / sampleRate);
        _history = new Complex[_lowPass.Length];
        _matched = Fir.Hann(2 * SamplesPerSymbolDecimated);
        _matchedHistory = new Complex[_matched.Length];
        StartFrequencyHz = FrequencyHz = frequencyHz;
    }

    /// <summary>The frequency the receiver started on.</summary>
    public double StartFrequencyHz { get; }

    private bool _heard;

    // Characters wait this many symbols before they are passed on, and are dropped if quality falls to DropLevel
    // meanwhile: quality takes about 36 symbols to fall that far after a signal ends, and the noise decoded in that time
    // is not shown. A weak signal's dips below the squelch do not lose what was already read, and a clean one (quality
    // 0.8 or more) is shown without waiting.
    private const int HoldSymbols = 48;
    private const double DropLevel = 0.1;
    private readonly Queue<(char Char, long Symbol)> _held = new();
    private long _symbols;

    /// <summary>The frequency it is on now (moves with AFC).</summary>
    public double FrequencyHz { get; private set; }

    /// <summary>Automatic frequency control. On by default.</summary>
    public bool Afc { get; set; } = true;

    /// <summary>How far AFC may move from <see cref="StartFrequencyHz"/>, in hertz.</summary>
    public double AfcRangeHz { get; init; } = 15;

    /// <summary>Signal quality, 0 (noise) to 1 (clean): the running average of cos(2Δφ).</summary>
    public double Quality { get; private set; }

    /// <summary>Quality at or above which received characters are passed on.</summary>
    public double SquelchLevel { get; set; } = 0.4;

    /// <summary>True while characters are being passed on.</summary>
    public bool SquelchOpen => Quality >= SquelchLevel;

    /// <summary>Feeds audio; returns the characters completed in it (empty when none).</summary>
    public string Process(ReadOnlySpan<float> samples)
    {
        var step = Math.Tau * FrequencyHz / _sampleRate;
        foreach (var s in samples)
        {
            _history[_historyPos] = new Complex(s * Math.Cos(_ncoPhase), -s * Math.Sin(_ncoPhase));
            _historyPos = (_historyPos + 1) % _history.Length;
            _ncoPhase += step;
            if (_ncoPhase > Math.Tau) _ncoPhase -= Math.Tau;
            if (++_decimationCount < _decimation) continue;
            _decimationCount = 0;
            Decimated(Filter(_history, _historyPos, _lowPass));
            step = Math.Tau * FrequencyHz / _sampleRate;
        }
        if (_out.Length == 0) return string.Empty;
        var text = _out.ToString();
        _out.Clear();
        return text;
    }

    private static Complex Filter(Complex[] history, int pos, double[] taps)
    {
        double re = 0, im = 0;
        var n = history.Length;
        for (var i = 0; i < n; i++)
        {
            var h = history[(pos + i) % n];
            re += h.Real * taps[i];
            im += h.Imaginary * taps[i];
        }
        return new Complex(re, im);
    }

    private void Decimated(Complex x)
    {
        _matchedHistory[_matchedPos] = x;
        _matchedPos = (_matchedPos + 1) % _matchedHistory.Length;
        var z = Filter(_matchedHistory, _matchedPos, _matched);

        var index = (int)_bitClock;
        _sync[index] = 0.8 * _sync[index] + 0.2 * z.Magnitude;
        double diff = 0, total = 0;
        for (var i = 0; i < SamplesPerSymbolDecimated / 2; i++)
        {
            diff += _sync[i] - _sync[i + SamplesPerSymbolDecimated / 2];
            total += _sync[i] + _sync[i + SamplesPerSymbolDecimated / 2];
        }
        // The correction is stable with the magnitude peak where the clock wraps (indexes 15 and 0 balance each other),
        // and the symbol is taken there; the middle of the clock is the null of a phase reversal.
        _bitClock -= total == 0 ? 0 : diff / total / 5;
        _bitClock += 1;
        if (_bitClock < 0) _bitClock += SamplesPerSymbolDecimated;
        if (_bitClock < SamplesPerSymbolDecimated) return;
        _bitClock -= SamplesPerSymbolDecimated;
        Symbol(z);
    }

    private void Symbol(Complex z)
    {
        var d = z * Complex.Conjugate(_previous);
        _previous = z;
        if (d == Complex.Zero) return;
        var angle = d.Phase;
        Quality += (Math.Cos(2 * angle) - Quality) / 16;
        var bit = d.Real > 0 ? 1 : 0;

        if (SquelchOpen) _heard = true;
        // Once a signal has been heard and gone, AFC holds its frequency so noise cannot walk it away before it returns.
        if (Afc && (Quality > 0.25 || !_heard))
        {
            // Squaring removes the modulation, leaving twice the rotation per symbol: unambiguous to ±baud/4 (±7.8 Hz in
            // PSK31). Before the signal is locked the loop is slow, so noise only nudges it.
            // While locked, only clean symbols (within 30 degrees of a decision point) steer it, which keeps the noise
            // at the end of an over from pulling it off the signal before the squelch closes.
            var error = (d * d).Phase / 2;
            var hz = error * _baud / Math.Tau;
            var gain = Quality <= 0.25 ? 1.0 / 32 : Math.Cos(2 * angle) > 0.5 ? 1.0 / 16 : 0;
            FrequencyHz = Math.Clamp(FrequencyHz + hz * gain, StartFrequencyHz - AfcRangeHz, StartFrequencyHz + AfcRangeHz);
        }
        _symbols++;
        if (Quality < DropLevel) _held.Clear();
        // A clean signal (quality near 1) releases its characters at once; noise never gets there.
        while (_held.Count > 0 && (_symbols - _held.Peek().Symbol >= HoldSymbols || Quality >= 0.8)) Emit(_held.Dequeue().Char);
        Bit(bit);
    }

    private void Bit(int bit)
    {
        if (bit == 1)
        {
            var gap = _zeros == 1;
            _zeros = 0;
            if (_code < 0) return;
            if (gap) _code <<= 1;
            _code = (_code << 1) | 1;
            // No Varicode is this long: idle carrier or noise. Nothing is read until the next 00 gap, so the end of a
            // long run of ones (the postamble) is not taken for a short character such as "e".
            if (_code > 0x7FFF) _code = -1;
            return;
        }
        if (++_zeros != 2) return;
        if (_code > 0 && SquelchOpen && Varicode.TryDecode(_code, out var c)) _held.Enqueue((c, _symbols));
        _code = 0;
    }

    private void Emit(char c)
    {
        if (c == '\n' || c >= ' ' && c < 127) _out.Append(c);
    }
}
