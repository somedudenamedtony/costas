// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Rig;

/// <summary>
/// What a radio's Hamlib driver says it can do, from rigctld's <c>\dump_caps</c>: the levels and functions it reads and
/// sets, and its preamp, attenuator and AGC steps. A setting is only offered when the driver both reads and sets it.
/// </summary>
public sealed class RigCapabilities
{
    private readonly HashSet<string> _getLevels;
    private readonly HashSet<string> _setLevels;
    private readonly HashSet<string> _getFuncs;
    private readonly HashSet<string> _setFuncs;

    private RigCapabilities(HashSet<string> getLevels, HashSet<string> setLevels, HashSet<string> getFuncs, HashSet<string> setFuncs,
                            IReadOnlyList<int> preamps, IReadOnlyList<int> attenuators, IReadOnlyList<int> agcLevels)
    {
        _getLevels = getLevels;
        _setLevels = setLevels;
        _getFuncs = getFuncs;
        _setFuncs = setFuncs;
        Preamps = preamps;
        Attenuators = attenuators;
        AgcLevels = agcLevels;
    }

    /// <summary>Preamp steps in dB, without off.</summary>
    public IReadOnlyList<int> Preamps { get; }

    /// <summary>Attenuator steps in dB, without off.</summary>
    public IReadOnlyList<int> Attenuators { get; }

    /// <summary>The AGC speeds the radio has, as Hamlib's numbers, in the order the driver lists them.</summary>
    public IReadOnlyList<int> AgcLevels { get; }

    /// <summary>Hamlib's name for a setting, as <c>l</c>/<c>L</c> (levels) or <c>u</c>/<c>U</c> (functions) take it.</summary>
    public static string HamlibName(RigControl c) => c switch
    {
        RigControl.AfGain => "AF",
        RigControl.RfGain => "RF",
        RigControl.Preamp => "PREAMP",
        RigControl.Attenuator => "ATT",
        RigControl.Agc => "AGC",
        RigControl.NoiseBlanker => "NB",
        RigControl.NoiseReduction => "NR",
        RigControl.Tuner => "TUNER",
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };

    /// <summary>True for the on/off functions; false for levels.</summary>
    public static bool IsFunction(RigControl c) => c is RigControl.NoiseBlanker or RigControl.NoiseReduction or RigControl.Tuner;

    /// <summary>True when the driver reads and sets <paramref name="c"/> and, for steps, lists at least one.</summary>
    public bool Supports(RigControl c)
    {
        var name = HamlibName(c);
        var ok = IsFunction(c) ? _getFuncs.Contains(name) && _setFuncs.Contains(name) : _getLevels.Contains(name) && _setLevels.Contains(name);
        return ok && c switch
        {
            RigControl.Preamp => Preamps.Count > 0,
            RigControl.Attenuator => Attenuators.Count > 0,
            RigControl.Agc => AgcLevels.Count > 1,
            _ => true,
        };
    }

    /// <summary>The values <paramref name="c"/> accepts: off plus the steps, or the AGC speeds; empty for gains and switches.</summary>
    public IReadOnlyList<int> Options(RigControl c) => c switch
    {
        RigControl.Preamp => [0, .. Preamps],
        RigControl.Attenuator => [0, .. Attenuators],
        RigControl.Agc => AgcLevels,
        _ => [],
    };

    /// <summary>
    /// Reads the capabilities from <c>\dump_caps</c>, given its "Key: value" lines by key ("Get level", "Set level",
    /// "Get functions", "Set functions", "Preamp", "Attenuator", "AGC levels"). Missing lines mean not supported.
    /// </summary>
    public static RigCapabilities Parse(Func<string, string?> line)
    {
        static HashSet<string> Names(string? s) => new(
            (s ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Split('(')[0]),
            StringComparer.OrdinalIgnoreCase);
        // "10dB 20dB", or "None".
        static List<int> Steps(string? s) => [.. (s ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => int.TryParse(t.Replace("dB", string.Empty, StringComparison.OrdinalIgnoreCase), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0)
            .Where(d => d > 0).Distinct()];
        // "0=OFF 2=FAST 5=MEDIUM 3=SLOW 6=AUTO". A radio with no AGC choice lists only NONE, which is left out.
        var agc = (line("AGC levels") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Split('='))
            .Where(p => p.Length == 2 && !string.Equals(p[1], "NONE", StringComparison.OrdinalIgnoreCase))
            .Select(p => int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && AgcName(v) is not null ? v : (int?)null)
            .OfType<int>().Distinct().ToList();
        return new RigCapabilities(Names(line("Get level")), Names(line("Set level")), Names(line("Get functions")), Names(line("Set functions")),
            Steps(line("Preamp")), Steps(line("Attenuator")), agc);
    }

    /// <summary>A short name for one of Hamlib's AGC numbers ("Fast"), or null for one Costas does not know.</summary>
    public static string? AgcName(int level) => level switch
    {
        0 => "Off",
        1 => "Super fast",
        2 => "Fast",
        3 => "Slow",
        4 => "User",
        5 => "Mid",
        6 => "Auto",
        7 => "Long",
        8 => "On",
        _ => null,
    };
}
