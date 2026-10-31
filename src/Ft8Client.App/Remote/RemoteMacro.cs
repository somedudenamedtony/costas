// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Remote;

/// <summary>A PSK or RTTY macro key on the phone.</summary>
/// <param name="Key">The function key, 1 for F1.</param>
/// <param name="Caption">"F3 Exchange".</param>
/// <param name="Text">The macro as written, with its fields.</param>
/// <param name="Next">True for the next step's macro, shown in the accent colour.</param>
public sealed record RemoteMacro(int Key, string Caption, string Text, bool Next);
