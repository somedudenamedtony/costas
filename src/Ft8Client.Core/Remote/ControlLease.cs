// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Remote;

/// <summary>
/// Which remote device is the control point, and whether its link is still alive. A device takes the lease with any
/// command; operator input at the desk releases it. While a device holds the lease it must send a heartbeat at least every
/// <see cref="LinkTimeout"/>, or the station stops transmitting (47 CFR 97.213(b) asks for 3 minutes at most).
/// </summary>
public sealed class ControlLease
{
    /// <summary>How long the controlling device may be silent before transmit is stopped: one FT8 slot.</summary>
    public static readonly TimeSpan LinkTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The Tx watchdog is never longer than this while a remote device has control.</summary>
    public const int WatchdogCapMinutes = 3;

    private DateTime _lastSeenUtc;

    /// <summary>The device in control, or null when the desk is.</summary>
    public string? Holder { get; private set; }

    /// <summary>True while a remote device has control.</summary>
    public bool IsHeld => Holder is not null;

    /// <summary>Gives control to a device (it sent a command).</summary>
    public void Take(string deviceId, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        Holder = deviceId;
        _lastSeenUtc = nowUtc;
    }

    /// <summary>Records that a device is still there. Ignored for any device but the holder.</summary>
    public void Heartbeat(string deviceId, DateTime nowUtc)
    {
        if (string.Equals(deviceId, Holder, StringComparison.Ordinal) && nowUtc > _lastSeenUtc) _lastSeenUtc = nowUtc;
    }

    /// <summary>Gives control back to the desk.</summary>
    public void Release() => Holder = null;

    /// <summary>True when a device holds control and has been silent for <see cref="LinkTimeout"/> or longer.</summary>
    public bool LinkLost(DateTime nowUtc) => Holder is not null && nowUtc - _lastSeenUtc >= LinkTimeout;

    /// <summary>The watchdog to use: the operator's setting, capped while a remote device has control.</summary>
    public int WatchdogMinutes(int configured) => IsHeld ? Math.Min(configured, WatchdogCapMinutes) : configured;
}
