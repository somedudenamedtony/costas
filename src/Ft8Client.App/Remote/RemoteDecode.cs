// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>One line of the band activity (raw decodes), as on the desk.</summary>
/// <param name="Utc">Slot time, "203100".</param>
/// <param name="Db">SNR, or "Tx" for my own transmission.</param>
/// <param name="Dt">Time offset.</param>
/// <param name="Hz">Audio offset.</param>
/// <param name="Message">The message.</param>
/// <param name="Country">The sender's country.</param>
/// <param name="Kind">"me" (to me), "cq", "tx" (mine), or blank.</param>
/// <param name="Sender">The sender's call, for opening its details.</param>
public sealed record RemoteDecode(string Utc, string Db, string Dt, string Hz, string Message, string Country, string Kind, string? Sender);
