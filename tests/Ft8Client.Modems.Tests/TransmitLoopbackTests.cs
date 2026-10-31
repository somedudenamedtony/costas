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
using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.Modems.Tests;

/// <summary>
/// Transmit audio, built a piece at a time as the keyboard transmitter builds it, read back by small reference decoders
/// written here from the mode definitions (and fldigi's conventions), not by Costas's own demodulators, so a mistake both
/// sides share (a polarity, a bit order, a symbol clock) still fails. The same audio was read word for word by fldigi
/// 4.2.03 on 2026-10-31 (PSK31, PSK63, RTTY at 44.1 and 48 kHz; docs/08-open-questions.md V26).
/// </summary>
public class TransmitLoopbackTests
{
    private const string Text = "CQ CQ CQ DE W7LIT W7LIT PSE K\n73 TEST 599 de w7lit 0123456789 ?/.,\n";
    private const double CarrierHz = 1000;

    // The over as KeyboardTransmitter sends it: Start, characters with idles between some, Stop.
    private static float[] Over(IKeyboardModulator mod, string text)
    {
        var audio = new List<float>(mod.Start());
        for (var i = 0; i < text.Length; i++)
        {
            audio.AddRange(mod.Send(mod.Normalize(text[i])));
            if (i % 7 == 0) audio.AddRange(mod.Idle());
        }
        audio.AddRange(mod.Stop());
        return [.. audio];
    }

    [Theory]
    [InlineData(Mode.Psk31, 48_000)]
    [InlineData(Mode.Psk31, 44_100)]
    [InlineData(Mode.Psk63, 48_000)]
    [InlineData(Mode.Psk63, 44_100)]
    public void PskOver_ReferenceDecoder_ReadsTextExactly(Mode mode, int rate)
    {
        var audio = Over(new PskModulator(mode, rate, CarrierHz), Text);
        ReadPsk(audio, rate, PskMode.Baud(mode)).Should().Be(Text);
    }

    [Theory]
    [InlineData(48_000)]
    [InlineData(44_100)]
    public void RttyOver_ReferenceDecoder_ReadsTextWithMarkHigh(int rate)
    {
        var audio = Over(new RttyModulator(RttyFormat.Standard, rate, CarrierHz, 1.0), Text);
        // Amateur RTTY: mark is the higher RF frequency, which in USB (DATA-U) is the higher audio tone.
        ReadRtty(audio, rate, markHz: CarrierHz + 85, spaceHz: CarrierHz - 85).Trim('\r', '\n').Should().Be(Text.ToUpperInvariant().Replace("\n", "\r\n").TrimEnd('\r', '\n'));
    }

    [Fact]
    public void RttyOver_ReferenceDecoderWithTonesSwapped_ReadsGarbage()
    {
        var audio = Over(new RttyModulator(RttyFormat.Standard, 48_000, CarrierHz, 1.0), Text);
        ReadRtty(audio, 48_000, markHz: CarrierHz - 85, spaceHz: CarrierHz + 85).Should().NotContain("W7LIT");
    }

    [Fact]
    public void RttyOver_Tones_AreMarkAndSpace170HzApart()
    {
        var rate = 48_000;
        var audio = Over(new RttyModulator(RttyFormat.Standard, rate, CarrierHz, 1.0), Text);
        // The opening mark carrier (a quarter second) is the higher tone.
        var lead = audio.AsSpan(0, rate / 5);
        Power(lead, rate, CarrierHz + 85).Should().BeGreaterThan(100 * Power(lead, rate, CarrierHz - 85));
    }

    // BPSK by differential phase at the symbol boundaries, where the shaped carrier is at full level: the same phase
    // as the boundary before is a 1, a reversal a 0. Varicode characters end at "00" (the first 0 is already shifted in).
    private static string ReadPsk(float[] audio, int rate, double baud)
    {
        var symbol = rate / baud;
        var half = (int)(symbol / 4);
        var text = new StringBuilder();
        Complex? last = null;
        var code = 0;
        var zeros = 0;
        for (var k = 0; ; k++)
        {
            var at = (int)Math.Floor(k * symbol);
            if (at + half >= audio.Length) break;
            var z = Mix(audio, rate, CarrierHz, Math.Max(0, at - half), at + half);
            if (last is { } prev)
            {
                var one = (z * Complex.Conjugate(prev)).Real > 0;
                if (one)
                {
                    code = (code << 1) | 1;
                    zeros = 0;
                }
                else if (++zeros == 2)
                {
                    if (code != 0 && Varicode.TryDecode(code >> 1, out var c)) text.Append(c);
                    code = 0;
                }
                else
                {
                    code <<= 1;
                }
            }
            last = z;
        }
        return text.ToString();
    }

    // Asynchronous RTTY: mark and space strength over a sliding half bit, a mark-to-space edge starts a character, the
    // five data bits are read at their middles (lowest first), then ITA2 with letters/figures and unshift-on-space.
    private static string ReadRtty(float[] audio, int rate, double markHz, double spaceHz)
    {
        var bit = rate / 45.45;
        var window = (int)(bit / 2);
        var mark = Strength(audio, rate, markHz, window);
        var space = Strength(audio, rate, spaceHz, window);
        bool IsMark(double at) => (int)at < mark.Length && mark[(int)at] > space[(int)at];
        var text = new StringBuilder();
        var figures = false;
        var n = 0;
        while (n < audio.Length - 8 * bit)
        {
            if (IsMark(n) || mark[n] + space[n] < 0.1)
            {
                n++;
                continue;
            }
            // n is the start bit's edge (the windows are centred, so no delay to allow for).
            if (IsMark(n + bit / 2))
            {
                n++;
                continue;
            }
            var code = 0;
            for (var b = 0; b < 5; b++)
                if (IsMark(n + (1.5 + b) * bit)) code |= 1 << b;
            if (IsMark(n + 6.5 * bit))
            {
                switch (code)
                {
                    case 0x1F: figures = false; break;
                    case 0x1B: figures = true; break;
                    case 0x04: text.Append(' '); figures = false; break;
                    default:
                        var c = (figures ? Figs : Ltrs)[code];
                        if (c != '_') text.Append(c);
                        break;
                }
            }
            n += (int)(7 * bit);
        }
        return text.ToString();
    }

    // ITA2 with US figures (fldigi's tables), written out here rather than taken from Ita2. '_' is unused.
    private const string Ltrs = "_E\nA SIU\rDRJNFCKTZLWHYPQOBG_MXV_";
    private const string Figs = "_3\n- _87\r$4',!:(5\")2#6019?&_./;_";

    private static double[] Strength(float[] audio, int rate, double hz, int window)
    {
        var w = Math.Tau * hz / rate;
        var re = new double[audio.Length + 1];
        var im = new double[audio.Length + 1];
        for (var i = 0; i < audio.Length; i++)
        {
            re[i + 1] = re[i] + audio[i] * Math.Cos(w * i);
            im[i + 1] = im[i] - audio[i] * Math.Sin(w * i);
        }
        var output = new double[audio.Length];
        for (var i = 0; i < audio.Length; i++)
        {
            var a = Math.Max(0, i - window / 2);
            var b = Math.Min(audio.Length, i + window / 2);
            var r = re[b] - re[a];
            var q = im[b] - im[a];
            output[i] = Math.Sqrt(r * r + q * q) / window;
        }
        return output;
    }

    private static Complex Mix(float[] audio, int rate, double hz, int from, int to)
    {
        var w = Math.Tau * hz / rate;
        var z = Complex.Zero;
        for (var i = from; i < to; i++) z += audio[i] * new Complex(Math.Cos(w * i), -Math.Sin(w * i));
        return z;
    }

    private static double Power(ReadOnlySpan<float> audio, int rate, double hz)
    {
        var w = Math.Tau * hz / rate;
        double r = 0, q = 0;
        for (var i = 0; i < audio.Length; i++)
        {
            r += audio[i] * Math.Cos(w * i);
            q -= audio[i] * Math.Sin(w * i);
        }
        return r * r + q * q;
    }
}
