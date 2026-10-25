// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Rig;

namespace Ft8Client.App.Remote;

/// <summary>Radio settings the phone can change. Only settings: nothing here keys the radio.</summary>
public interface IRemoteRig
{
    /// <summary>The power setting as last read from the radio, or null when it has not been read or cannot be.</summary>
    RigPower? Power { get; }

    /// <summary>Reads the power setting from the radio again.</summary>
    Task<RigPower?> RefreshAsync();

    /// <summary>Changes the power setting and reads it back; null when the radio did not take it.</summary>
    Task<RigPower?> SetPowerAsync(int watts);
}
