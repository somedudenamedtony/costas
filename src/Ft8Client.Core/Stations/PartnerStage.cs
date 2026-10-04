// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Stations;

/// <summary>How far a station's contact with someone else has got.</summary>
public enum PartnerStage
{
    /// <summary>Just sent a grid.</summary>
    Grid,

    /// <summary>Reports exchanged.</summary>
    Reports,

    /// <summary>Signing off.</summary>
    SignOff,

    /// <summary>An exchange the app does not interpret.</summary>
    Other,
}
