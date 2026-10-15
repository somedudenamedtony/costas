// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Services;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Settings;
using Serilog;

namespace Ft8Client.App.Engine;

/// <summary>
/// Sets the transmitter's audio level for the current band, and when automatic level is on moves it after each
/// transmission from the ALC the radio reported (<see cref="TxLevelTuner"/>), saving the new level for that band.
/// The level only changes between transmissions. A radio that reports no ALC leaves the level as it is.
/// </summary>
public sealed class TxLevelController
{
    /// <summary>
    /// Automatic level rises at most this far above the level the operator set in Settings. It falls at most
    /// <see cref="AudioSettings.AutoLowestBelowDb"/> below it.
    /// </summary>
    public const double AutoHeadroomDb = 6;

    private readonly Transmitter _tx;
    private readonly AppSettingsAccessor _settings;
    private readonly object _gate = new();
    private readonly Dictionary<string, TxLevelState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TxLevelChange> _lastChange = new(StringComparer.Ordinal);

    /// <summary>Drives <paramref name="tx"/>'s level from <paramref name="settings"/>, applying it now.</summary>
    public TxLevelController(Transmitter tx, AppSettingsAccessor settings)
    {
        _tx = tx;
        _settings = settings;
        Apply();
    }

    /// <summary>The transmit level for <paramref name="band"/>: its own if it has one, else the level in Settings.</summary>
    public static double LevelFor(AudioSettings audio, string band)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return audio.TxGainByBand.TryGetValue(band, out var g) ? g : audio.TxGainDb;
    }

    /// <summary>Sets the transmitter to the current band's level (after a band or settings change).</summary>
    public void Apply()
    {
        var s = _settings.Current;
        _tx.GainDb = LevelFor(s.Profile.Audio, s.Operating.Band);
    }

    /// <summary>
    /// Adjusts the current band's level from one transmission's meters and returns the result to show, with the ALC
    /// advice saying what Costas did when it lowered the level itself.
    /// </summary>
    public TxMeterResult OnMeasured(TxMeterResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var s = _settings.Current;
        var audio = s.Profile.Audio;
        if (!audio.AutoTxLevel || result.Alc is not { } alc) return result;
        if (alc is < 0 or > 1)
        {
            // Hamlib reads ALC from 0 to 1. Anything else is not an ALC reading (SWR, read from a reply out of step, is
            // 1 or more and walked the level down to silence): leave the level alone.
            Log.Warning("Automatic transmit level ignored an ALC reading of {Alc} (outside 0 to 1)", alc);
            return result;
        }
        var band = s.Operating.Band;
        var gain = LevelFor(audio, band);
        TxLevelStep step;
        TxLevelChange? previous;
        lock (_gate)
        {
            // A level changed by hand since the last transmission starts afresh.
            var known = _states.TryGetValue(band, out var st) && st.GainDb == gain;
            previous = known ? _lastChange.GetValueOrDefault(band) : null;
            step = TxLevelTuner.Next(known ? st! : new TxLevelState(gain), alc, audio.TxGainDb + AutoHeadroomDb,
                audio.TxGainDb - AudioSettings.AutoLowestBelowDb);
            _states[band] = step.State;
            _lastChange[band] = step.Change;
        }
        var next = step.State.GainDb;
        if (next != gain)
        {
            _settings.Update(x => x.Profile.Audio.TxGainByBand[band] = next);
            Apply();
            Log.Information("Automatic transmit level on {Band}: ALC {Alc:0.00}, level {From:0.0} → {To:0.0} dB", band, alc, gain, next);
        }
        else if (step.Change == TxLevelChange.AtMaximum && previous != TxLevelChange.AtMaximum)
        {
            Log.Information("Automatic transmit level on {Band}: no ALC at its highest level {Level:0.0} dB", band, gain);
        }

        // At the lowest automatic level the operator's advice stays: Costas did not lower anything.
        if (step.Change != TxLevelChange.Lowered || next >= gain || result.Warning is not { } w) return result;
        var done = $"Costas lowered its audio level to {next.ToString("0.0", CultureInfo.InvariantCulture)} dB for the next transmission.";
        return result with { Warning = w.Replace(TxMeterCheck.AlcAdvice, done, StringComparison.Ordinal) };
    }
}
