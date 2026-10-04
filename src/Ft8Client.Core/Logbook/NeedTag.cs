// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>Why the operator needs a station, from their log.</summary>
public enum NeedTag
{
    /// <summary>The entity has no contact in the log on any band.</summary>
    NewCountry,

    /// <summary>The entity has contacts, but none on this band.</summary>
    NewBand,

    /// <summary>The station's 4-character grid has no contact in the log.</summary>
    NewGrid,

    /// <summary>This call has no contact on this band and mode.</summary>
    NewCall,

    /// <summary>This call has a contact on this band and mode.</summary>
    Worked,
}
