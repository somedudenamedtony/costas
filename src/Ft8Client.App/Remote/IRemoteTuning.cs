// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;

namespace Ft8Client.App.Remote;

/// <summary>The station settings the phone can change, the same as the band menu and Tx offset on the desk.</summary>
public interface IRemoteTuning
{
    /// <summary>The bands in the frequency table for a mode.</summary>
    IReadOnlyList<string> Bands(Mode mode);

    /// <summary>Changes band and mode, and tunes the radio.</summary>
    Task SetBandAsync(string band, Mode mode);

    /// <summary>Sets the Tx offset in hertz, or automatic with null, and saves it.</summary>
    void SetTxOffset(int? hz);
}
