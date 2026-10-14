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
    private readonly int _symbolSamples;
    private readonly double _step;
    private double _phase;
    private double _amplitude = 1;

    /// <param name="mode">PSK31 or PSK63.</param>
    /// <param name="sampleRate">Output sample rate.</param>
    /// <param name="frequencyHz">Audio frequency of the carrier.</param>
    /// <param name="level">Peak level, 0 to 1.</param>
    public PskModulator(Mode mode, int sampleRate, double frequencyHz, double level = 0.5)
    {
        _symbolSamples = PskMode.SymbolSamples(mode, sampleRate);
        _step = 2 * Math.PI * frequencyHz / sampleRate;
        Level = level;
    }

    /// <summary>Peak output level.</summary>
    public double Level { get; }

    /// <summary>Symbols of reversals before the text and of carrier after it (32 is one second of PSK31).</summary>
    public int IdleSymbols { get; init; } = 32;

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
        var bits = new string('0', IdleSymbols) + TextBits(text) + new string('1', IdleSymbols);
        var output = new float[(bits.Length + 1) * _symbolSamples];
        var n = 0;
        foreach (var b in bits) n = Symbol(output, n, b == '0');
        for (var i = 0; i < _symbolSamples; i++, n++)
        {
            var fade = 0.5 + 0.5 * Math.Cos(Math.PI * i / _symbolSamples);
            output[n] = (float)(Level * _amplitude * fade * Math.Cos(_phase));
            _phase += _step;
        }
        return output;
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
        var carrier = Bits(new string('1', IdleSymbols / 2));
        var output = new float[carrier.Length + _symbolSamples];
        carrier.CopyTo(output, 0);
        var n = carrier.Length;
        for (var i = 0; i < _symbolSamples; i++, n++)
        {
            var fade = 0.5 + 0.5 * Math.Cos(Math.PI * i / _symbolSamples);
            output[n] = (float)(Level * _amplitude * fade * Math.Cos(_phase));
            _phase += _step;
        }
        return output;
    }

    /// <inheritdoc />
    public bool CanSend(char c) => Varicode.CanSend(c);

    /// <inheritdoc />
    public char Normalize(char c) => c;

    private float[] Bits(string bits)
    {
        var output = new float[bits.Length * _symbolSamples];
        var n = 0;
        foreach (var b in bits) n = Symbol(output, n, b == '0');
        return output;
    }

    private int Symbol(float[] output, int n, bool reverse)
    {
        var from = _amplitude;
        var to = reverse ? -from : from;
        for (var i = 0; i < _symbolSamples; i++, n++)
        {
            var env = (from + to) / 2 + (from - to) / 2 * Math.Cos(Math.PI * i / _symbolSamples);
            output[n] = (float)(Level * env * Math.Cos(_phase));
            _phase += _step;
            if (_phase > Math.Tau) _phase -= Math.Tau;
        }
        _amplitude = to;
        return n;
    }
}
