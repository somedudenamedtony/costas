// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>What happened to an INSERT.</summary>
public enum QrzInsertOutcome
{
    /// <summary>Inserted; a log id was returned.</summary>
    Inserted,

    /// <summary>QRZ already had it; counts as uploaded.</summary>
    Duplicate,

    /// <summary>QRZ refused it for a reason that retrying will not fix (e.g. outside the logbook's date range).</summary>
    Rejected,

    /// <summary>The key lacks privileges.</summary>
    AuthFailed,

    /// <summary>A transient failure (network, server); retry later.</summary>
    Transient,
}
