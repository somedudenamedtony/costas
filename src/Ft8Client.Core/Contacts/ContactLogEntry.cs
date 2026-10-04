// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>A contact to log.</summary>
/// <param name="Call">DX call.</param>
/// <param name="Grid">DX grid, if known.</param>
/// <param name="ReportSent">Report I sent.</param>
/// <param name="ReportReceived">Report I received, if any.</param>
/// <param name="TimeOnUtc">Start of my first transmission in the contact.</param>
/// <param name="TimeOffUtc">When it was logged.</param>
public sealed record ContactLogEntry(string Call, string? Grid, int ReportSent, int? ReportReceived, DateTime TimeOnUtc, DateTime TimeOffUtc);
