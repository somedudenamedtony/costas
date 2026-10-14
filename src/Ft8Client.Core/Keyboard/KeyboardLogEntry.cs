// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Contacts;

namespace Ft8Client.Core.Keyboard;

/// <summary>A keyboard-mode contact to log: the entry, plus what the partner sent that FT8 has no room for.</summary>
/// <param name="Entry">Call, grid, reports and times.</param>
/// <param name="Name">Their name, if found or clicked.</param>
/// <param name="Qth">Where they are, if found or clicked.</param>
/// <param name="OffsetHz">Their audio offset: the frequency logged is the dial plus this.</param>
public sealed record KeyboardLogEntry(ContactLogEntry Entry, string? Name, string? Qth, int OffsetHz);
