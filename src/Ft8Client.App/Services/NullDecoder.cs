// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;

namespace Ft8Client.App.Services;

/// <summary>Used when jt9 cannot be found: every slot reports the reason instead of decoding.</summary>
public sealed class NullDecoder(string reason) : IDecoder
{
    /// <inheritdoc />
    public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct) =>
        Task.FromResult(new DecodeResult([], TimeSpan.Zero, DecodeStatus.Failed, reason));
}
