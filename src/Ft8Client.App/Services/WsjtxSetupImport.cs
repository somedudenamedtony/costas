// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.RegularExpressions;
using Ft8Client.Audio.Backends;
using Ft8Client.Data.Import;
using Ft8Client.Rig;

namespace Ft8Client.App.Services;

/// <summary>
/// Turns a WSJT-X configuration into what Setup fills in, so someone moving from WSJT-X does not set up the radio and
/// sound card again. Nothing is tested or keyed here: each Setup step still runs its own check.
/// </summary>
public static partial class WsjtxSetupImport
{
    /// <summary>Matches the settings against the radios Hamlib lists and the sound devices present.</summary>
    public static WsjtxSetupPlan Plan(WsjtxSettings s, IReadOnlyList<HamlibModel> models, IReadOnlyList<AudioDevice> inputs, IReadOnlyList<AudioDevice> outputs)
    {
        var notCopied = new List<string>();
        var audioOnly = string.Equals(s.RigName, WsjtxSettings.NoRig, StringComparison.OrdinalIgnoreCase);
        HamlibModel? model = null;
        if (!audioOnly && s.RigName.Length > 0)
        {
            // WSJT-X names a Hamlib radio "<manufacturer> <model>".
            model = models.FirstOrDefault(m => string.Equals($"{m.Manufacturer} {m.Model}", s.RigName, StringComparison.OrdinalIgnoreCase));
            if (model is null) notCopied.Add($"Radio: WSJT-X uses \"{s.RigName}\", which is not in Costas's radio list. Choose it in step 2.");
        }
        var ptt = s.Ptt;
        if (audioOnly && ptt is "rts" or "dtr")
        {
            notCopied.Add($"PTT: WSJT-X keys by {ptt.ToUpperInvariant()} with no CAT radio, which Costas does not do. It will use VOX.");
            ptt = "vox";
        }
        var input = MatchDevice(s.SoundIn, inputs);
        var output = MatchDevice(s.SoundOut, outputs);
        if (s.SoundIn.Length > 0 && input is null) notCopied.Add($"Audio input: \"{s.SoundIn}\" is not connected now.");
        if (s.SoundOut.Length > 0 && output is null) notCopied.Add($"Audio output: \"{s.SoundOut}\" is not connected now.");
        var pttPort = s.PttPort.Length == 0 || string.Equals(s.PttPort, "CAT", StringComparison.OrdinalIgnoreCase) || s.PttPort == s.SerialPort
            ? string.Empty
            : s.PttPort;
        return new WsjtxSetupPlan(s.MyCall, s.MyGrid, audioOnly, model, s.SerialPort, s.NetworkAddress,
            s.Baud?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, ptt, pttPort, input, output, notCopied);
    }

    /// <summary>
    /// The device WSJT-X saved, by name: an exact match first, then one name starting with the other (Windows can cut
    /// a long device name short in one list and not the other).
    /// </summary>
    public static AudioDevice? MatchDevice(string name, IReadOnlyList<AudioDevice> devices)
    {
        name = name.Trim();
        if (name.Length == 0) return null;
        return devices.FirstOrDefault(d => string.Equals(d.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
               ?? devices.FirstOrDefault(d => d.Name.Trim().StartsWith(name, StringComparison.OrdinalIgnoreCase)
                                              || name.StartsWith(d.Name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Contacts in an ADIF file (records ending in <c>&lt;eor&gt;</c>).</summary>
    public static int CountRecords(string adif) => EndOfRecord().Count(adif);

    /// <summary>
    /// The line Setup shows when WSJT-X is found: "WSJT-X is set up on this computer: K1ABC, FN42, Icom IC-7300 on COM4,
    /// 1,234 contacts in its log."
    /// </summary>
    public static string Summary(WsjtxSettings? s, int? contacts)
    {
        var parts = new List<string>();
        if (s is not null)
        {
            if (s.MyCall.Length > 0) parts.Add(s.MyCall);
            if (s.MyGrid.Length > 0) parts.Add(s.MyGrid);
            if (string.Equals(s.RigName, WsjtxSettings.NoRig, StringComparison.OrdinalIgnoreCase)) parts.Add("no CAT radio");
            else if (s.RigName.Length > 0) parts.Add(s.RigName + (s.SerialPort.Length > 0 ? $" on {s.SerialPort}" : s.NetworkAddress.Length > 0 ? $" at {s.NetworkAddress}" : string.Empty));
        }
        if (contacts is { } n) parts.Add(n == 1 ? "1 contact in its log" : $"{n.ToString("N0", CultureInfo.InvariantCulture)} contacts in its log");
        return parts.Count == 0 ? "WSJT-X is set up on this computer." : $"WSJT-X is set up on this computer: {string.Join(", ", parts)}.";
    }

    [GeneratedRegex("<eor>", RegexOptions.IgnoreCase)]
    private static partial Regex EndOfRecord();
}
