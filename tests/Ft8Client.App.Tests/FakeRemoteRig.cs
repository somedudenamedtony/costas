// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.Remote;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>A radio power setting for the remote page, recording the changes asked for.</summary>
internal sealed class FakeRemoteRig : IRemoteRig
{
    public RigPower? Power { get; set; } = new(50, 100);

    /// <summary>The radio's own lowest setting; a lower one reads back as this.</summary>
    public int MinWatts { get; set; } = 5;

    /// <summary>True to act as a radio that refuses the change.</summary>
    public bool Refuse { get; set; }

    public List<int> Sets { get; } = [];

    public Task<RigPower?> RefreshAsync() => Task.FromResult(Power);

    public Task<RigPower?> SetPowerAsync(int watts)
    {
        Sets.Add(watts);
        if (Refuse || Power is null) return Task.FromResult<RigPower?>(null);
        Power = Power with { Watts = Math.Max(watts, MinWatts) };
        return Task.FromResult<RigPower?>(Power);
    }
}
