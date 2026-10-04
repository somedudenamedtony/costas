// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>State of one box in the conversation strip.</summary>
public enum StepState
{
    /// <summary>Not reached yet.</summary>
    Upcoming,

    /// <summary>The step being sent or waited for.</summary>
    Now,

    /// <summary>Sent or received.</summary>
    Done,
}
