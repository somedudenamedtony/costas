// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core;
using Ft8Client.Rig;

namespace Ft8Client.App.Engine;

/// <summary>
/// Radio settings that belong to a keyboard mode (docs/09-keyboard-modes.md section 3). RTTY sends at full power for
/// the whole over, so its power is capped at a share of what the operator set, and put back on leaving RTTY unless the
/// operator has turned the knob since. In PSK and RTTY the speech processor is read and a warning given when it is on;
/// Costas never changes it.
/// </summary>
public sealed class KeyboardRadio
{
    // Power settings closer than this are the same setting (the FT-710 steps in 1 W of 100).
    private const double Same = 0.015;

    private readonly object _gate = new();
    private double? _restore;
    private double? _capped;

    /// <summary>Shown when the speech processor is on in a keyboard mode.</summary>
    public const string ProcessorWarning = "The radio's speech processor is on. It distorts PSK and RTTY: turn it off on the radio.";

    /// <summary>The power setting to put back on leaving RTTY, while capped.</summary>
    public double? RestoreLevel
    {
        get { lock (_gate) return _restore; }
    }

    /// <summary>
    /// Applies the radio settings for <paramref name="mode"/>: caps the power at <paramref name="rttyPercent"/> % in RTTY
    /// and restores it otherwise. Returns a warning to show, or null.
    /// </summary>
    public async Task<string?> ApplyAsync(IRig rig, Mode mode, int rttyPercent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rig);
        string? note = null;
        if (mode == Mode.Rtty && rttyPercent is > 0 and < 100) note = await CapAsync(rig, rttyPercent / 100.0, ct).ConfigureAwait(false);
        else await RestoreAsync(rig, ct).ConfigureAwait(false);
        if (ModeInfo.IsSlotted(mode)) return note;
        return await rig.ReadSpeechProcessorAsync(ct).ConfigureAwait(false) == true ? ProcessorWarning : note;
    }

    /// <summary>Puts the operator's power back if it is still capped (on leaving RTTY and on exit).</summary>
    public async Task RestoreAsync(IRig rig, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rig);
        double restore, capped;
        lock (_gate)
        {
            if (_restore is not { } r || _capped is not { } c) return;
            restore = r;
            capped = c;
            _restore = null;
            _capped = null;
        }
        // Turned by hand since it was capped: the operator's new setting stays.
        var now = await rig.ReadPowerLevelAsync(ct).ConfigureAwait(false);
        if (now is { } n && Math.Abs(n - capped) > Same) return;
        await rig.SetPowerLevelAsync(restore, ct).ConfigureAwait(false);
    }

    private async Task<string?> CapAsync(IRig rig, double share, CancellationToken ct)
    {
        var now = await rig.ReadPowerLevelAsync(ct).ConfigureAwait(false);
        double restore;
        lock (_gate)
        {
            // Already capped: the knob may have moved since (then that is the operator's new setting), or the share changed.
            restore = _restore is { } r && _capped is { } c && now is { } n && Math.Abs(n - c) <= Same ? r : now ?? -1;
        }
        if (restore < 0) return "Costas could not read the radio's power, so RTTY is not capped. Check the power before sending.";
        var cap = Math.Round(restore * share, 3);
        if (!await rig.SetPowerLevelAsync(cap, ct).ConfigureAwait(false))
            return "The radio did not take the RTTY power cap. Check the power before sending.";
        lock (_gate)
        {
            _restore = restore;
            _capped = cap;
        }
        return null;
    }
}
