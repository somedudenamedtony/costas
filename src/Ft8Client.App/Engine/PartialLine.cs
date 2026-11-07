// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Engine;

/// <summary>The text a keyboard-mode channel has read since its last finished line: shown as it arrives.</summary>
/// <param name="OffsetHz">The channel's audio frequency.</param>
/// <param name="Call">The channel's station, when known.</param>
/// <param name="Text">The characters so far.</param>
public sealed record PartialLine(int OffsetHz, string? Call, string Text);
