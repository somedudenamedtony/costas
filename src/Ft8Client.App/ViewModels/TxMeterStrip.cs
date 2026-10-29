// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>
/// What the transmit meter strip shows. A text is null when the radio does not report that meter. Bars run 0 to 1:
/// power as a share of the radio's most, ALC on Hamlib's scale, and SWR from 1.0 (empty) to 3.0 (full).
/// </summary>
public sealed record TxMeterStrip(
    bool Live,
    string Caption,
    string Tip,
    string? Watts,
    double WattsBar,
    string? Alc,
    double AlcBar,
    TextKind AlcKind,
    string? Swr,
    double SwrBar,
    TextKind SwrKind);
