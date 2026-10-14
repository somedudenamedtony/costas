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
/// operator has turned the knob since. The cap is saved as it is made (through the callback given), so a restart after
/// a crash restores the operator's power instead of capping the cap again; a restore the radio refuses is kept and tried
/// again. Calls are serialised: a band change, a settings change and a radio reconnect never cap at the same time. In
/// PSK and RTTY the speech processor is read and a warning given when it is on; Costas never changes it.
/// </summary>
public sealed class KeyboardRadio
{
    // Power settings closer than this are the same setting (the FT-710 steps in 1 W of 100).
    private const double Same = 0.015;

    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly Action<RttyPowerCap?>? _save;
    private RttyPowerCap? _cap;

    /// <summary>Shown when the speech processor is on in a keyboard mode.</summary>
    public const string ProcessorWarning = "The radio's speech processor is on. It distorts PSK and RTTY: turn it off on the radio.";

    /// <summary>
    /// Creates the radio settings, starting from a cap <paramref name="saved"/> by an earlier run (null for none).
    /// <paramref name="save"/> is called with the cap each time it is made, changed or cleared.
    /// </summary>
    public KeyboardRadio(RttyPowerCap? saved = null, Action<RttyPowerCap?>? save = null)
    {
        _cap = saved;
        _save = save;
    }

    /// <summary>The power setting to put back on leaving RTTY, while capped.</summary>
    public double? RestoreLevel => Volatile.Read(ref _cap)?.Restore;

    /// <summary>The cap on the radio now, or null.</summary>
    public RttyPowerCap? Cap => Volatile.Read(ref _cap);

    /// <summary>
    /// Applies the radio settings for <paramref name="mode"/>: caps the power at <paramref name="rttyPercent"/> % in RTTY
    /// and restores it otherwise. Returns a warning to show, or null.
    /// </summary>
    public async Task<string?> ApplyAsync(IRig rig, Mode mode, int rttyPercent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rig);
        await _busy.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var note = mode == Mode.Rtty && rttyPercent is > 0 and < 100
                ? await CapAsync(rig, rttyPercent / 100.0, ct).ConfigureAwait(false)
                : await RestoreCoreAsync(rig, ct).ConfigureAwait(false);
            if (ModeInfo.IsSlotted(mode)) return note;
            if (await rig.ReadSpeechProcessorAsync(ct).ConfigureAwait(false) != true) return note;
            return note is null ? ProcessorWarning : $"{note} {ProcessorWarning}";
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>
    /// Puts the operator's power back if it is still capped (on leaving RTTY and on exit). Returns null when done or
    /// nothing was capped, or a note when the radio did not take it (the cap is kept, so the next call tries again).
    /// </summary>
    public async Task<string?> RestoreAsync(IRig rig, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rig);
        await _busy.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RestoreCoreAsync(rig, ct).ConfigureAwait(false);
        }
        finally
        {
            _busy.Release();
        }
    }

    private async Task<string?> RestoreCoreAsync(IRig rig, CancellationToken ct)
    {
        if (_cap is not { } cap) return null;
        // Turned by hand since it was capped: the operator's new setting stays.
        var now = await rig.ReadPowerLevelAsync(ct).ConfigureAwait(false);
        if (now is { } n && Math.Abs(n - cap.Capped) > Same)
        {
            SetCap(null);
            return null;
        }
        if (!await rig.SetPowerLevelAsync(cap.Restore, ct).ConfigureAwait(false))
            return $"The radio did not take back its power after RTTY: set it to {cap.Restore * 100:0} % on the radio.";
        SetCap(null);
        return null;
    }

    private async Task<string?> CapAsync(IRig rig, double share, CancellationToken ct)
    {
        var now = await rig.ReadPowerLevelAsync(ct).ConfigureAwait(false);
        var before = _cap;
        // Already capped (in this run or before a crash): the knob may have moved since (then that is the operator's new
        // setting), or the share changed. Either way the cap comes from the operator's setting, never from the cap.
        var restore = before is { } c && now is { } n && Math.Abs(n - c.Capped) <= Same ? c.Restore : now ?? -1;
        if (restore < 0) return "Costas could not read the radio's power, so RTTY is not capped. Check the power before sending.";
        var cap = Math.Round(restore * share, 3);
        // Saved before the radio is touched: a crash from here on still knows the operator's power.
        SetCap(new RttyPowerCap(restore, cap));
        if (!await rig.SetPowerLevelAsync(cap, ct).ConfigureAwait(false))
        {
            // The radio is still where it was: still capped as before, or at the operator's own setting.
            SetCap(before is not null && restore == before.Restore ? before : null);
            return "The radio did not take the RTTY power cap. Check the power before sending.";
        }
        // A radio with coarse steps rounds the setting: what it reports is what a later restore compares with.
        if (await rig.ReadPowerLevelAsync(ct).ConfigureAwait(false) is { } readBack && readBack != cap)
            SetCap(new RttyPowerCap(restore, readBack));
        return null;
    }

    private void SetCap(RttyPowerCap? cap)
    {
        if (cap == _cap) return;
        Volatile.Write(ref _cap, cap);
        _save?.Invoke(cap);
    }
}
