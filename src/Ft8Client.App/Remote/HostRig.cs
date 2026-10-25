// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Rig;
using Serilog;

namespace Ft8Client.App.Remote;

/// <summary>Radio settings from the phone, sent to whichever radio the station has open now.</summary>
public sealed class HostRig(Func<IRig> rig) : IRemoteRig
{
    private RigPower? _power;

    /// <inheritdoc />
    public RigPower? Power => Volatile.Read(ref _power);

    /// <inheritdoc />
    public async Task<RigPower?> RefreshAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var p = await rig().ReadPowerAsync(cts.Token).ConfigureAwait(false);
        Volatile.Write(ref _power, p);
        return p;
    }

    /// <inheritdoc />
    public async Task<RigPower?> SetPowerAsync(int watts)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var p = await rig().SetPowerWattsAsync(watts, cts.Token).ConfigureAwait(false);
        Log.Information("Phone set radio power to {Watts} W; radio reads {Read}", watts, p?.Watts);
        if (p is not null) Volatile.Write(ref _power, p);
        return p;
    }
}
