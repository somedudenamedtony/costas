// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Rtty;

/// <summary>
/// RTTY transmit audio (AFSK): each character is a start bit (space), five ITA2 bits, lowest first, and 1.5 stop bits
/// (mark), the tones switched without a phase jump. An over opens with mark carrier, a few LTRS and CR LF, and the
/// figures shift is sent again after each space, since receivers with unshift-on-space drop back to letters there.
/// </summary>
public sealed class RttyModulator : IKeyboardModulator
{
    private readonly int _sampleRate;
    private readonly double _markStep;
    private readonly double _spaceStep;
    private readonly double _bitSamples;
    private readonly List<float> _out = [];
    private double _phase;
    private double _owed;
    private bool _figures;

    /// <param name="format">Speed, shift and polarity.</param>
    /// <param name="sampleRate">Output sample rate.</param>
    /// <param name="centreHz">Audio frequency midway between the tones.</param>
    /// <param name="level">Peak level, 0 to 1.</param>
    public RttyModulator(RttyFormat format, int sampleRate, double centreHz, double level = 0.5)
    {
        _sampleRate = sampleRate;
        _markStep = Math.Tau * format.MarkHz(centreHz) / sampleRate;
        _spaceStep = Math.Tau * format.SpaceHz(centreHz) / sampleRate;
        _bitSamples = sampleRate / format.Baud;
        Level = level;
    }

    /// <summary>Peak output level.</summary>
    public double Level { get; }

    /// <summary>LTRS characters sent before the text and after it.</summary>
    public int IdleCharacters { get; init; } = 4;

    /// <summary>
    /// A whole over. Throws for characters ITA2 cannot send: the caller checks <see cref="Ita2.CanSend"/> and refuses the
    /// message rather than changing it.
    /// </summary>
    public float[] Modulate(string text)
    {
        _out.Clear();
        Tone(true, 0.5 * _sampleRate);
        for (var i = 0; i < IdleCharacters; i++) Character(Ita2.Letters);
        Character(0x08);
        Character(0x02);
        var figures = false;
        foreach (var c in text)
        {
            if (!Ita2.TryEncode(c, out var code, out var caseOf)) throw new ArgumentException($"RTTY cannot send '{c}'.", nameof(text));
            if (caseOf is { } f && f != figures)
            {
                Character(f ? Ita2.Figures : Ita2.Letters);
                figures = f;
            }
            Character(code);
            // Unshift-on-space: the receiver is back in letters after a space.
            if (code == Ita2.Space) figures = false;
        }
        for (var i = 0; i < IdleCharacters; i++) Character(Ita2.Letters);
        return [.. _out];
    }

    /// <inheritdoc />
    public float[] Start()
    {
        _out.Clear();
        _figures = false;
        Tone(true, 0.25 * _sampleRate);
        Character(Ita2.Letters);
        Character(Ita2.Letters);
        Character(0x08);
        Character(0x02);
        return [.. _out];
    }

    /// <inheritdoc />
    public float[] Send(char c)
    {
        if (!Ita2.TryEncode(c, out var code, out var caseOf)) throw new ArgumentException($"RTTY cannot send '{c}'.", nameof(c));
        _out.Clear();
        if (caseOf is { } f && f != _figures)
        {
            Character(f ? Ita2.Figures : Ita2.Letters);
            _figures = f;
        }
        Character(code);
        if (code == Ita2.Space) _figures = false;
        return [.. _out];
    }

    /// <inheritdoc />
    public float[] Idle()
    {
        _out.Clear();
        Character(Ita2.Letters);
        _figures = false;
        return [.. _out];
    }

    /// <inheritdoc />
    public float[] Stop()
    {
        _out.Clear();
        Character(Ita2.Letters);
        Character(Ita2.Letters);
        // A short fade on the last mark so the carrier does not end with a click.
        var start = _out.Count;
        Tone(true, 0.02 * _sampleRate);
        for (var i = start; i < _out.Count; i++) _out[i] *= (float)(0.5 + 0.5 * Math.Cos(Math.PI * (i - start) / (_out.Count - start)));
        return [.. _out];
    }

    /// <inheritdoc />
    public bool CanSend(char c) => Ita2.CanSend(c);

    /// <inheritdoc />
    public char Normalize(char c) => char.ToUpperInvariant(c);

    private void Character(int code)
    {
        Tone(false, _bitSamples);
        for (var b = 0; b < 5; b++) Tone(((code >> b) & 1) == 1, _bitSamples);
        Tone(true, 1.5 * _bitSamples);
    }

    // Appends a tone for a (fractional) number of samples, carrying the remainder so the bit timing stays exact.
    private void Tone(bool mark, double samples)
    {
        _owed += samples;
        var n = (int)_owed;
        _owed -= n;
        var step = mark ? _markStep : _spaceStep;
        for (var i = 0; i < n; i++)
        {
            _out.Add((float)(Level * Math.Cos(_phase)));
            _phase += step;
            if (_phase > Math.Tau) _phase -= Math.Tau;
        }
    }
}
