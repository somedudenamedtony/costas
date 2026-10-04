// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>Outcome of decoding one slot.</summary>
/// <param name="Decodes">The decodes, in decoder order.</param>
/// <param name="Elapsed">Wall time spent decoding.</param>
/// <param name="Status">Whether the run completed, timed out or failed.</param>
/// <param name="Message">Detail for a failure; empty on success.</param>
public sealed record DecodeResult(IReadOnlyList<Decode> Decodes, TimeSpan Elapsed, DecodeStatus Status, string Message = "")
{
    /// <summary>True when the decoder ran to completion.</summary>
    public bool Succeeded => Status == DecodeStatus.Ok;
}
