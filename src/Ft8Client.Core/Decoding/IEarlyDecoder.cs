// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>
/// A decoder that can start on a slot before it ends, as WSJT-X does: an early pass over the first 11.8 s of an FT8
/// slot finds most signals while the slot is still running, and the full decode at the cut-off only has to look for
/// the rest. The full decode of a slot that had an early pass still returns every decode of the slot.
/// </summary>
public interface IEarlyDecoder
{
    /// <summary>True when this decoder runs an early pass for the mode (at <c>SlotMark.EarlyDecode</c>, 11.85 s into an FT8 slot).</summary>
    bool HasEarlyPass(Mode mode);

    /// <summary>
    /// Decodes the part of a slot received so far (the rest of <see cref="SlotAudio.Samples12k"/> is silence).
    /// Never throws for decoder faults.
    /// </summary>
    Task<DecodeResult> DecodeEarlyAsync(SlotAudio partial, DecodeContext ctx, CancellationToken ct);
}
