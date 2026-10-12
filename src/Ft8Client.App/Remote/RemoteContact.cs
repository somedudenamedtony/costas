// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>The contact card on the phone.</summary>
/// <param name="Caption">"Calling", "Contact in progress", "Logged", "Abandoned".</param>
/// <param name="Call">DX call.</param>
/// <param name="Need">Need tag, or blank.</param>
/// <param name="Subline">Name · place · distance.</param>
/// <param name="Sent">Report sent, "--" until sent.</param>
/// <param name="Received">Report received, "--" until received.</param>
/// <param name="Active">The contact is still running.</param>
/// <param name="CanLogNow">Log now is allowed.</param>
/// <param name="Steps">The conversation strip.</param>
public sealed record RemoteContact(string Caption, string Call, string Need, string Subline, string Sent, string Received, bool Active, bool CanLogNow,
                                   IReadOnlyList<RemoteStep> Steps);
