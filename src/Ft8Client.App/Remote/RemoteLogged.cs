// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A contact just logged, for the phone's "Logged" notice; sent only while the notice should still show.</summary>
/// <param name="Id">Counts up per contact logged, so the phone shows each notice once.</param>
/// <param name="Call">The call logged.</param>
/// <param name="Need">"New call", as on the desk.</param>
/// <param name="Line">"Sent -12 · Received -08 · Indiana, USA".</param>
/// <param name="MsLeft">How long the notice still shows, in milliseconds.</param>
public sealed record RemoteLogged(long Id, string Call, string Need, string Line, int MsLeft);
