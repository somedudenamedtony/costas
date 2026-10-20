// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>
/// A decoder that can look for a SuperFox (a DXpedition running WSJT-X's SuperFox mode) beside its normal decode. In
/// SuperFox mode <c>jt9</c> decodes only the SuperFox signal, so the search runs separately and the normal decode of the
/// slot is unaffected.
/// </summary>
public interface ISuperFoxDecoder
{
    /// <summary>True when the SuperFox search can run for this mode (FT8, with a <c>jt9</c> that has the SuperFox decoder).</summary>
    bool CanDecodeSuperFox(Mode mode);

    /// <summary>
    /// Decodes a slot for a SuperFox only. Its messages come out as ordinary decode lines (<c>CQ KH1X</c>,
    /// <c>W7LIT KH1X -12</c>, <c>W7LIT KH1X RR73</c>) and a <c>$VERIFY$</c> line. Never throws for decoder faults.
    /// </summary>
    Task<DecodeResult> DecodeSuperFoxAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct);
}
