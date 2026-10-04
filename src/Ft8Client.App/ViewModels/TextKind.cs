// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>Which design token colours a piece of text. Colour is never the only signal: the text says it too.</summary>
public enum TextKind
{
    /// <summary>Primary text.</summary>
    Normal,

    /// <summary>Secondary text.</summary>
    Secondary,

    /// <summary>Accent: new band, new grid, selected.</summary>
    Accent,

    /// <summary>Caution: new country, upload waiting.</summary>
    Caution,

    /// <summary>Critical: transmitting, faults.</summary>
    Critical,

    /// <summary>Success: connected.</summary>
    Success,
}
