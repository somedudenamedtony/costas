// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Core.Keyboard;

/// <summary>What the text of one over (or the part read so far) says about who is sending.</summary>
/// <param name="Sender">The sending station, when a pattern named it.</param>
/// <param name="To">The station it is sending to, when <c>&lt;a&gt; DE &lt;b&gt;</c> named one.</param>
/// <param name="Cq">True when the over contains CQ or QRZ.</param>
/// <param name="Ended">True when the over ends with K, KN, BK or SK.</param>
public sealed record OverSummary(string? Sender, string? To, bool Cq, bool Ended);
