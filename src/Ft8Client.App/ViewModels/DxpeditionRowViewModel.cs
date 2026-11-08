// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One announced DXpedition on the Awards view.</summary>
/// <param name="Call">"H44MS".</param>
/// <param name="Entity">"Solomon Is".</param>
/// <param name="When">"Oct 3 – Nov 20 · 13 days left".</param>
/// <param name="Need">"New country", "Worked, not confirmed", "Worked", or blank when unknown.</param>
/// <param name="NewCountry">True for a country not in the log yet.</param>
/// <param name="Heard">"Heard now on 20 m, -12 dB" while it is being decoded, otherwise blank.</param>
/// <param name="Tip">QSL route and the listing's notes.</param>
public sealed record DxpeditionRowViewModel(string Call, string Entity, string When, string Need, bool NewCountry, string Heard, string Tip)
{
    /// <summary>The heard line is shown.</summary>
    public bool IsHeard => Heard.Length > 0;

    /// <summary>Need colour: accent for a new country.</summary>
    public TextKind NeedKind => NewCountry ? TextKind.Accent : TextKind.Secondary;

    /// <summary>Heard line colour.</summary>
    public TextKind HeardKind => TextKind.Accent;
}
