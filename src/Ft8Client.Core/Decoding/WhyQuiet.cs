// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Services;

namespace Ft8Client.Core.Decoding;

/// <summary>
/// Picks the one line the Operate screen shows after <see cref="QuietWatch.SlotsBeforeQuiet"/> received slots with no
/// decodes: the most likely reason, with a fix where there is one. Causes are checked in order of confidence: the receive
/// audio, the dial, the radio's mode, the radio's receive filter, the decoder, the clock. A radio that is not connected defers to the no-radio
/// banner, and with no radio set up (and no simulation) nothing is shown, since nothing real is being received. The
/// readout only advises: it never changes the radio. Pure.
/// </summary>
public static class WhyQuiet
{
    /// <summary>
    /// A dial further than this from the usual frequency counts as off. Within 1 kHz most of the 3 kHz FT8 or FT4
    /// sub-band is still in the receiver's passband, so the dial alone would not leave the band silent.
    /// </summary>
    public const long DialToleranceHz = 1_000;

    /// <summary>
    /// A receive filter narrower than this counts as too narrow. FT8 and FT4 signals spread over about 200 to 3000 Hz of
    /// audio, so below 2000 Hz a third or more of the sub-band is cut off; a common 2400 Hz SSB filter loses only the top
    /// few hundred hertz and is not flagged.
    /// </summary>
    public const int MinPassbandHz = 2_000;

    /// <summary>Hamlib modes that receive FT8 and FT4: USB data and plain USB (the fallback for radios without data mode).</summary>
    public static IReadOnlyList<string> GoodRigModes { get; } = ["PKTUSB", "USB"];

    /// <summary>The line to show, or null when nothing should be shown.</summary>
    public static QuietReadout? Explain(QuietFacts f)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (!ModeInfo.IsSlotted(f.Mode) || !f.RadioSetUp || f.EmptySlots < QuietWatch.SlotsBeforeQuiet) return null;
        var mode = ModeInfo.Name(f.Mode);
        var band = BandPlan.Display(f.Band);
        var radioUp = f.Radio == ServiceHealth.Ok;

        switch (f.Audio)
        {
            case InputLevelState.NoAudio:
                return new(QuietCause.NoAudio, "No decodes: no audio is arriving from the radio's sound device. Check that it is plugged in and that no other program is holding it.");
            case InputLevelState.Silence:
                return new(QuietCause.Silence, "No decodes: the radio's sound device sends only silence. Turn up the radio's USB audio output and check that it is not muted.");
            case InputLevelState.TooQuiet:
                var db = f.AudioDbfs is { } v ? $" ({v.ToString("0", CultureInfo.InvariantCulture)} dBFS)" : string.Empty;
                return new(QuietCause.TooQuiet, $"No decodes: the receive audio is too quiet{db}. Turn up the radio's USB audio output or the computer's recording level.");
            case InputLevelState.Clipping:
                return new(QuietCause.Clipping, "No decodes: the receive audio is clipping. Turn down the radio's USB audio output or the computer's recording level.");
        }

        if (radioUp && f.DialHz is { } dial && f.UsualDialHz is { } usual && Math.Abs(dial - usual) > DialToleranceHz)
            return new(QuietCause.DialOff, $"No decodes: the dial is on {DialText.Mhz(dial)}, not the usual {mode} frequency for {band}.", usual);

        if (radioUp && f.RigMode is { Length: > 0 } rigMode && !GoodRigModes.Contains(rigMode.Trim().ToUpperInvariant()))
        {
            var fix = f.UsualDialHz is { } hz ? $" Change it on the radio, or Go to {DialText.Mhz(hz)} sets it." : " Change it on the radio.";
            return new(QuietCause.WrongMode, $"No decodes: the radio is in {RigModeName(rigMode)}, and {mode} needs USB data.{fix}", f.UsualDialHz);
        }

        // Costas never sets the filter width (it sends mode changes with "passband unchanged"), so there is no Go to.
        if (radioUp && f.PassbandHz is { } width and > 0 && width < MinPassbandHz)
        {
            var hz = width.ToString(CultureInfo.InvariantCulture);
            return new(QuietCause.NarrowFilter,
                $"No decodes: the radio's receive filter is {hz} Hz wide, and {mode} signals spread over about 3000 Hz. Widen it on the radio to about 3000 Hz.");
        }

        if (f.Decoder is { Health: ServiceHealth.Down or ServiceHealth.Degraded } d)
        {
            var why = d.Message.Trim().TrimEnd('.');
            var state = d.Health == ServiceHealth.Down ? "is not running" : "is not working";
            return new(QuietCause.Decoder, $"No decodes: the decoder (jt9) {state}{(why.Length > 0 ? $": {why}" : string.Empty)}. Diagnostics has the details.");
        }

        if (f.ClockOffsetSeconds is { } offset && Math.Abs(offset) > f.ClockLimitSeconds)
        {
            var off = Math.Abs(offset).ToString("0.0", CultureInfo.InvariantCulture);
            var limit = f.ClockLimitSeconds.ToString("0.#", CultureInfo.InvariantCulture);
            return new(QuietCause.Clock, $"No decodes: your clock is {off} s off, and decodes need it within {limit} s. Sync the computer's clock, or turn on Clock correction in Settings › Transmitting.");
        }

        // Not connected: the no-radio banner already says so and what to do.
        if (!radioUp) return null;

        return new(QuietCause.NothingFound,
            $"No decodes in the last {f.EmptySlots} slots. Audio, dial, radio mode, filter, decoder and clock look right, so {band} may just be quiet.");
    }

    /// <summary>A Hamlib mode as the operator knows it: <c>PKTLSB</c> reads "LSB data".</summary>
    public static string RigModeName(string hamlibMode)
    {
        ArgumentNullException.ThrowIfNull(hamlibMode);
        var m = hamlibMode.Trim().ToUpperInvariant();
        return m.StartsWith("PKT", StringComparison.Ordinal) && m.Length > 3 ? $"{m[3..]} data" : m;
    }
}
