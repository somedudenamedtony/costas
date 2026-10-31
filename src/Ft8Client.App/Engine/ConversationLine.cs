// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.App.Engine;

/// <summary>One over in a PSK or RTTY conversation, as the conversation box shows it.</summary>
/// <param name="Utc">When it ended (mine) or was read (theirs).</param>
/// <param name="Who">The sender's call.</param>
/// <param name="Text">What was sent.</param>
/// <param name="Mine">True for what I sent (shown dimmed).</param>
public sealed record ConversationLine(DateTime Utc, string Who, string Text, bool Mine);
