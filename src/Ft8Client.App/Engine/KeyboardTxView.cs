// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Engine;

/// <summary>The keyboard transmitter's state for the screen.</summary>
/// <param name="Active">True while an over is on the air (or about to be).</param>
/// <param name="Sent">Text of this over already sent (shown dimmed).</param>
/// <param name="Waiting">Text queued and not yet sent.</param>
/// <param name="EndsAfter">True when the over returns to receive once <paramref name="Waiting"/> is sent.</param>
public sealed record KeyboardTxView(bool Active, string Sent, string Waiting, bool EndsAfter)
{
    /// <summary>Nothing on the air.</summary>
    public static KeyboardTxView Idle { get; } = new(false, string.Empty, string.Empty, false);
}
