// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>Highlight Callsign (type 13): colour a callsign in the decode list; a null colour cancels it.</summary>
/// <param name="TargetId">The client it is meant for.</param>
/// <param name="Callsign">The callsign.</param>
/// <param name="Background">Background colour; null cancels the highlight.</param>
/// <param name="Foreground">Text colour; null cancels the highlight.</param>
/// <param name="LastOnly">True to highlight it in the latest slot only.</param>
public sealed record WsjtxHighlight(string TargetId, string Callsign, WsjtxColor? Background, WsjtxColor? Foreground, bool LastOnly)
    : WsjtxInboundMessage(TargetId)
{
    /// <summary>True when this cancels highlighting for the call.</summary>
    public bool Clears => Background is null || Foreground is null;
}
