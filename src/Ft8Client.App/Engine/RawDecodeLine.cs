// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Messages;

namespace Ft8Client.App.Engine;

/// <summary>One line of the Raw decodes view.</summary>
/// <param name="Decode">The decode.</param>
/// <param name="Message">Parsed message.</param>
/// <param name="Country">Sender's entity name, if known.</param>
/// <param name="ToMe">Addressed to my call.</param>
/// <param name="Transmitted">A line for my own transmission.</param>
public sealed record RawDecodeLine(Decode Decode, ParsedMessage Message, string? Country, bool ToMe, bool Transmitted = false)
{
    /// <summary>True for CQ.</summary>
    public bool IsCq => Message.Kind == MessageKind.Cq;
}
