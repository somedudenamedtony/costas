// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Rig;

namespace Ft8Client.App.Remote;

/// <summary>Builds the phone's Radio tab from the session and the settings read from the radio.</summary>
public static class RemoteRadioBuilder
{
    /// <summary>The "radio" command's name for a setting: "afGain", "preamp".</summary>
    public static string Key(RigControl c)
    {
        var name = c.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>The setting a "radio" command names, or null for one Costas does not know.</summary>
    public static RigControl? Parse(string? key) =>
        Enum.GetValues<RigControl>().Cast<RigControl?>().FirstOrDefault(c => string.Equals(Key(c!.Value), key, StringComparison.Ordinal));

    /// <summary>Builds the tab.</summary>
    /// <param name="s">The session now.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="controls">The settings as last read from the radio.</param>
    /// <param name="canChange">False while transmitting.</param>
    /// <param name="maxWatts">The radio's most power, for the power bar, when known.</param>
    public static RemoteRadio Build(SessionSnapshot s, DateTime nowUtc, IReadOnlyList<RigControlValue> controls, bool canChange, int? maxWatts)
    {
        var tuning = TuneState.Tuning(s, nowUtc);
        var strip = TxMeterText.Strip(s.TxMetersLive, s.TxMeters, maxWatts);
        var meters = strip is null ? null : new RemoteMeters(strip.Live, strip.Caption, strip.Watts, strip.WattsBar, strip.Alc, strip.AlcBar,
            strip.AlcKind != TextKind.Normal, strip.Swr, strip.SwrBar,
            strip.SwrKind switch { TextKind.Critical => "critical", TextKind.Caution => "caution", _ => string.Empty }, strip.Tip);
        return new RemoteRadio(tuning ? "Stop tune" : "Tune", TuneState.CanTune(s, nowUtc), tuning, canChange,
            [.. controls.Select(Control)], meters);
    }

    private static RemoteRadioControl Control(RigControlValue v)
    {
        var key = Key(v.Control);
        switch (v.Control)
        {
            case RigControl.AfGain:
                return Level(key, "AF gain", v.Value, "The radio's speaker volume.");
            case RigControl.RfGain:
                return Level(key, "RF gain", v.Value, "Leave at 100% unless a very strong band needs it turned down.");
            case RigControl.Preamp:
            {
                // Hamlib lists preamps by gain (FT-710: 10 and 20 dB) or by number (Icom: 1 and 2); the radios call them AMP 1, AMP 2.
                var opts = v.Options.Select(o => new RemoteRadioOption(o, o == 0 ? "Off" : "Amp " + (Index(v.Options, o)))).ToList();
                return Choice(key, "receive", "Preamp", v.Value, opts, "Off is usually best on 40 m and below; a preamp helps on the higher bands.");
            }
            case RigControl.Attenuator:
            {
                var opts = v.Options.Select(o => new RemoteRadioOption(o, o == 0 ? "Off" : o.ToString(CultureInfo.InvariantCulture) + " dB")).ToList();
                return Choice(key, "receive", "Attenuator", v.Value, opts, string.Empty);
            }
            case RigControl.Agc:
            {
                var opts = v.Options.Select(o => new RemoteRadioOption(o, RigCapabilities.AgcName(o) ?? o.ToString(CultureInfo.InvariantCulture))).ToList();
                return Choice(key, "receive", "AGC", v.Value, opts, string.Empty);
            }
            case RigControl.NoiseBlanker:
                return Toggle(key, "Noise blanker", v.Value, "For pulse noise such as power lines or ignition.");
            case RigControl.NoiseReduction:
                return Toggle(key, "Noise reduction", v.Value, "DSP noise reduction can hide weak digital signals; usually best off.");
            default:
                return Toggle(key, "Antenna tuner", v.Value, "Puts the radio's tuner in line or bypasses it. It does not transmit; Tune does.");
        }
    }

    private static int Index(IReadOnlyList<int> options, int value)
    {
        var steps = options.Where(o => o != 0).ToList();
        return steps.IndexOf(value) + 1;
    }

    private static RemoteRadioControl Level(string key, string label, int value, string hint) =>
        new(key, "receive", label, "level", value, value.ToString(CultureInfo.InvariantCulture) + "%", [], hint);

    private static RemoteRadioControl Choice(string key, string group, string label, int value, List<RemoteRadioOption> options, string hint) =>
        new(key, group, label, "choice", value, options.FirstOrDefault(o => o.Value == value)?.Label ?? value.ToString(CultureInfo.InvariantCulture), options, hint);

    private static RemoteRadioControl Toggle(string key, string label, int value, string hint) =>
        new(key, "noise", label, "toggle", value, value != 0 ? "On" : "Off", [new RemoteRadioOption(0, "Off"), new RemoteRadioOption(1, "On")], hint);
}
