// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Engine;

/// <summary>
/// Auto-call (owner request 2026-11-09): with the operator's switch on, Costas calls the top station in Next in line,
/// and when that contact ends (logged, no reply, answered someone else) the next one, until the operator stops it.
/// FT8 and FT4 only. It is never on at start-up and is not saved. A call it starts is not operator input, so the Tx
/// watchdog ends a run that nobody is watching. Anything that stops transmitting (Esc, Halt Tx, Abandon), Call CQ, a
/// band or mode change, and any transmit refusal or fault turn it off.
/// </summary>
public sealed partial class Session
{
    private bool _autoCall;
    private readonly HashSet<string> _autoTried = new(StringComparer.Ordinal); // called in this run, not called again

    /// <summary>Turns Auto-call on or off (an operator action). Turning it on stops a CQ in progress.</summary>
    public void SetAutoCall(bool on)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            _engine.OperatorActivity(now);
            if (on == _autoCall) return;
            if (!on)
            {
                StopAutoCall(null);
            }
            else if (ModeInfo.IsSlotted(_config.Mode))
            {
                _autoCall = true;
                _autoTried.Clear();
                _fault = null;
                if (_engine.CallingCq) _engine.StopCq(now);
                // The station in contact now (called by hand) is not called again after it ends.
                if (_engine.DxCall is { } dx) _autoTried.Add(dx);
                AutoCallNow(now);
            }
        }
        AfterCommand();
    }

    // Under _gate. Ends the run; a reason (for a stop the operator did not ask for) goes to the bottom bar.
    private void StopAutoCall(string? reason)
    {
        if (!_autoCall) return;
        _autoCall = false;
        _autoTried.Clear();
        if (reason is not null) _fault = reason;
    }

    // Under _gate, after each decode. Calls the next station when nothing else is going on: no contact (a contact Costas
    // or the operator started runs to its end first), no CQ, no Tune, and no free text or extra 73 still to go.
    private void AutoCallNow(DateTime now)
    {
        if (!_autoCall) return;
        if (!ModeInfo.IsSlotted(_config.Mode))
        {
            StopAutoCall(null);
            return;
        }
        if (_engine.DxCall is { } dx) _autoTried.Add(dx);
        if (_engine.InContact || _engine.CallingCq || _engine.ExtraPending || _rigTuningUntil > now) return;
        if (_engine.WatchdogExpired(now))
        {
            StopAutoCall($"Auto-call stopped: no click for {CappedContact().WatchdogMinutes} minutes (Tx watchdog).");
            return;
        }
        var line = Ranker.Rank(RankInputAt(_tracker.Stations.ToList(), now)).Line;
        if (AutoCall.Next(line, _autoTried)?.Station is not { Parity: { } parity } s) return;
        _autoTried.Add(s.Call);
        _callWhenFree.RemoveAll(c => Callsign.EqualsCall(c, s.Call));
        if (s.RunsFox) HoundCallOffset(SlotMath.Opposite(parity));
        _engine.CallStation(s.Call, s.Grid, s.LastSnr, parity, s.OffsetHz, now, fox: s.RunsFox, byOperator: false);
        _fault = null;
    }
}
