// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A row of Next in line or Repeats on the phone, with the same columns as the Operate view.</summary>
/// <param name="Call">Its call.</param>
/// <param name="Where">Place and distance.</param>
/// <param name="Need">Need tag, or blank.</param>
/// <param name="Doing">What it is doing now.</param>
/// <param name="Db">SNR at my station.</param>
/// <param name="Button">"Call", "Answer" for a station calling me, or blank when it cannot be called now.</param>
/// <param name="HearsYou">Hears you.</param>
/// <param name="Chance">Chance of a contact.</param>
/// <param name="LastHeard">"45 seconds ago".</param>
/// <param name="LastWorked">"Today · 20 m", or "Never".</param>
/// <param name="ChanceReason">What the chance is based on, "hears you at -07", as beside Chance on the desk.</param>
public sealed record RemoteStation(string Call, string Where, string Need, string Doing, string Db, string Button,
                                   string HearsYou = "", string Chance = "", string LastHeard = "", string LastWorked = "", string ChanceReason = "");
