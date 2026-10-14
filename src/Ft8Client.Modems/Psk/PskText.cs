// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Psk;

/// <summary>Characters received on one channel.</summary>
/// <param name="Channel">The channel's number, stable while it lives.</param>
/// <param name="FrequencyHz">The channel's audio frequency when the text arrived.</param>
/// <param name="Text">The characters.</param>
public sealed record PskText(int Channel, double FrequencyHz, string Text);
