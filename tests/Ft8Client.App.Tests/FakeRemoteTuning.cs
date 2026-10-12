// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Remote;
using Ft8Client.Core;

namespace Ft8Client.App.Tests;

/// <summary>Records band and offset changes asked for by the remote page.</summary>
internal sealed class FakeRemoteTuning : IRemoteTuning
{
    public List<string> Calls { get; } = [];

    public IReadOnlyList<string> Bands(Mode mode) => mode == Mode.Ft8 ? ["40m", "20m", "6m"] : ["40m", "20m"];

    public Task SetBandAsync(string band, Mode mode)
    {
        Calls.Add($"band {band} {ModeInfo.Name(mode)}");
        return Task.CompletedTask;
    }

    public void SetTxOffset(int? hz) => Calls.Add(hz is { } v ? $"offset {v}" : "offset auto");
}
