// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core;

namespace Ft8Client.App.Remote;

/// <summary>Band and Tx offset changes from the phone, done the way the desk does them.</summary>
public sealed class HostTuning(AppHost host) : IRemoteTuning
{
    /// <inheritdoc />
    public IReadOnlyList<string> Bands(Mode mode) => [.. host.Frequencies.Bands(mode)];

    /// <inheritdoc />
    public Task SetBandAsync(string band, Mode mode) => host.SetBandAsync(band, mode);

    /// <inheritdoc />
    public void SetTxOffset(int? hz)
    {
        host.Session.SetTxOffset(hz);
        host.Settings.Update(s => s.Operating.TxOffset = hz is { } v ? v.ToString(CultureInfo.InvariantCulture) : "auto");
    }
}
