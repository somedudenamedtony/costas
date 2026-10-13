// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One award card on the Awards view.</summary>
/// <param name="Title">"DXCC".</param>
/// <param name="About">"Countries, any band".</param>
/// <param name="Headline">"187 worked · 142 confirmed".</param>
/// <param name="Goal">"100 confirmed for the award · 340 in all".</param>
/// <param name="Confirmed">Confirmed share of the goal, 0 to 1.</param>
/// <param name="Worked">Worked share of the goal, 0 to 1.</param>
/// <param name="Missing">"Still needed: Alaska, Hawaii", or blank.</param>
/// <param name="MissingTip">The full list for the tooltip, or blank.</param>
/// <param name="Unconfirmed">"Worked, not confirmed yet: Ohio", or blank.</param>
public sealed record AwardCardViewModel(string Title, string About, string Headline, string Goal, double Confirmed, double Worked,
                                        string Missing, string MissingTip, string Unconfirmed)
{
    /// <summary>The missing line is shown.</summary>
    public bool HasMissing => Missing.Length > 0;

    /// <summary>The unconfirmed line is shown.</summary>
    public bool HasUnconfirmed => Unconfirmed.Length > 0;
}
