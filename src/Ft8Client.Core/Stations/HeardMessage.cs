// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Stations;

/// <summary>A decode with its parsed message.</summary>
/// <param name="Decode">The raw decode.</param>
/// <param name="Message">The parsed message.</param>
public sealed record HeardMessage(Decode Decode, ParsedMessage Message)
{
    /// <summary>Parses a decode.</summary>
    public static HeardMessage From(Decode d) => new(d, MessageParser.Parse(d.Text));
}
