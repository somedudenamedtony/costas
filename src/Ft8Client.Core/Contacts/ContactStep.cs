// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>One box of the conversation strip.</summary>
/// <param name="Mine">True for my transmissions.</param>
/// <param name="Message">The literal message (sent, received, or expected).</param>
/// <param name="State">Upcoming, now or done.</param>
/// <param name="Count">Times sent or received; shown as "×N" above 1.</param>
/// <param name="TimeUtc">When it was sent or received.</param>
public sealed record ContactStep(bool Mine, string Message, StepState State, int Count, DateTime? TimeUtc);
