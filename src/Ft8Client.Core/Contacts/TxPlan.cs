// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>What to transmit in a slot.</summary>
/// <param name="SlotStartUtc">The slot.</param>
/// <param name="Message">Message text, exactly as shown to the operator.</param>
/// <param name="DxCall">The station it is for, or null for CQ.</param>
/// <param name="DxOffsetHz">The DX station's offset (for decoding its reply), if any.</param>
public sealed record TxPlan(DateTime SlotStartUtc, string Message, string? DxCall, int? DxOffsetHz);
