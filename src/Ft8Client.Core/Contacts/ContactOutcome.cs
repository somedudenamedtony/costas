// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>How a contact ended.</summary>
public enum ContactOutcome
{
    /// <summary>Still going.</summary>
    InProgress,

    /// <summary>Logged.</summary>
    Logged,

    /// <summary>No valid reply within the retry limit.</summary>
    NoReply,

    /// <summary>The operator abandoned it or halted transmitting.</summary>
    Abandoned,

    /// <summary>The Tx watchdog stopped it.</summary>
    Watchdog,
}
