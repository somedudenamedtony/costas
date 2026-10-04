// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>A recent report of my signal.</summary>
/// <param name="Call">Receiver.</param>
/// <param name="Where">Place.</param>
/// <param name="Db">Report.</param>
/// <param name="Age">"2 min ago".</param>
public sealed record ReportRowViewModel(string Call, string Where, string Db, string Age);
