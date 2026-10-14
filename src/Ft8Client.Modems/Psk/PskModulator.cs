// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;
using Ft8Client.Core;

namespace Ft8Client.Modems.Psk;

/// <summary>
/// BPSK31/63 transmit audio: Varicode bits, where 0 is a phase reversal and 1 is no change, each reversal shaped with
/// a cosine across the whole symbol so the signal stays narrow. An over starts with reversals (the idle) and ends with
/// steady carrier, then a short fade (docs/09-keyboard-modes.md section 8: 1 s of reversals, 0.5 s of carrier).
/// </summary>
public sealed class PskModulator : IKeyboardModulator
{
    private readonly double _symbolSamples;
    private readonly double _step;
    private double _phase;
    private double _amplitude = 1;
    private double _clock;

    /// <param name="mode">PSK31 or PSK63.</param>
    /// <param name="sampleRate">Output sample rate. Any rate works: at 44.1 kHz a symbol is 1,411.2 samples, so symbol
    /// lengths alternate by one sample and the symbol rate stays exact.</param>
    /// <param name="frequencyHz">Audio frequency of the carrier.</param>
    /// <param name="level">Peak level, 0 to 1.</param>
    public PskModulator(Mode mode, int sampleRate, double frequencyHz, double level = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        var baud = PskMode.Baud(mode);
        _symbolSamples = sampleRate / baud;
        _step = 2 * Math.PI * frequencyHz / sampleRate;
        Level = level;
        IdleSymbols = (int)Math.Ceiling(baud);
    }

    /// <summary>Peak output level.</summary>
    public double Level { get; }

    /// <summary>
    /// Symbols of reversals before the text: one second by default (32 for PSK31, 63 for PSK63). An over ends with half as
    /// many of steady carrier.
    /// </summary>
    public int IdleSymbols { get; init; }

    /// <summary>The bits for a text, each character followed by the "00" separator.</summary>
    public static string TextBits(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text) sb.Append(Varicode.Bits(c)).Append("00");
        return sb.ToString();
    }

    /// <summary>A whole over: idle reversals, the text, carrier, and a fade.</summary>
    public float[] Modulate(string text)
    {
        var output = new List<float>();
        Bits(new string('0', IdleSymbols) + TextBits(text) + new string('1', IdleSymbols), output);
        Fade(output);
        return [.. output];
    }

    /// <inheritdoc />
    public float[] Start() => Bits(new string('0', IdleSymbols));

    /// <inheritdoc />
    public float[] Send(char c)
    {
        if (!CanSend(c)) throw new ArgumentException($"PSK cannot send '{c}'.", nameof(c));
        return Bits(Varicode.Bits(c) + "00");
    }

    /// <inheritdoc />
    public float[] Idle() => Bits("00");

    /// <inheritdoc />
    public float[] Stop()
    {
        var output = new List<float>();
        Bits(new string('1', IdleSymbols / 2), output);
        Fade(output);
        return [.. output];
    }

    /// <inheritdoc />
    public bool CanSend(char c) => Varicode.CanSend(c);

    /// <inheritdoc />
    public char Normalize(char c) => c;

    private float[] Bits(string bits)
    {
        var output = new List<float>();
        Bits(bits, output);
        return [.. output];
    }

    private void Bits(string bits, List<float> output)
    {
        foreach (var b in bits) Symbol(output, b == '0');
    }

    // The next symbol's length in samples: whole samples that keep the running symbol clock exact.
    private int NextLength()
    {
        var start = Math.Floor(_clock);
        _clock += _symbolSamples;
        return (int)(Math.Floor(_clock) - start);
    }

    private void Symbol(List<float> output, bool reverse)
    {
        var from = _amplitude;
        var to = reverse ? -from : from;
        var length = NextLength();
        for (var i = 0; i < length; i++)
        {
            var env = (from + to) / 2 + (from - to) / 2 * Math.Cos(Math.PI * i / length);
            output.Add((float)(Level * env * Math.Cos(_phase)));
            Advance();
        }
        _amplitude = to;
    }

    // One symbol of raised-cosine fade to silence.
    private void Fade(List<float> output)
    {
        var length = NextLength();
        for (var i = 0; i < length; i++)
        {
            var fade = 0.5 + 0.5 * Math.Cos(Math.PI * i / length);
            output.Add((float)(Level * _amplitude * fade * Math.Cos(_phase)));
            Advance();
        }
    }

    private void Advance()
    {
        _phase += _step;
        if (_phase > Math.Tau) _phase -= Math.Tau;
    }
}
