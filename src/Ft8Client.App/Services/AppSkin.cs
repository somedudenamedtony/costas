// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Services;

/// <summary>A named desktop skin. Classic leaves Fluent's system accent in place.</summary>
public sealed record AppSkin(string Id, string Name, string Description, SkinPalette Light, SkinPalette Dark)
{
    public static IReadOnlyList<AppSkin> All { get; } =
    [
        new("classic", "Classic", "The original neutral surfaces and your system accent. Preview shows the default blue accent.",
            new("#F3F3F3", "#FFFFFF", "#E0E0E0", "#D1D1D1", "#1A1A1A", "#5F5F5F", "#005FB8", "#005FB8", "#E5F1FB", "#EBEBEB"),
            new("#202020", "#2B2B2B", "#3A3A3A", "#4A4A4A", "#F2F2F2", "#AAAAAA", "#005FB8", "#A6D8FF", "#1F3A55", "#353535")),
        new("ocean", "Ocean", "Cool blue surfaces with a deep blue accent.",
            new("#EAF1F8", "#F8FBFF", "#CBD9E8", "#A8BDD3", "#182D43", "#4D637A", "#1764A0", "#175C94", "#D9EAF9", "#DFE8F2"),
            new("#111E2D", "#1A2B3E", "#30465E", "#4A627D", "#EBF3FC", "#ADBED2", "#2676B5", "#8BC8FF", "#233F5B", "#283D53")),
        new("forest", "Forest", "Soft green surfaces with a pine green accent.",
            new("#ECF2ED", "#F9FCF9", "#CCDCCE", "#A6BFAA", "#1D3225", "#506958", "#256A49", "#225F41", "#DCEEE1", "#E0EAE2"),
            new("#14211B", "#1E3027", "#354D3F", "#506F5B", "#ECF5EE", "#AEC6B5", "#2D7853", "#96D7AD", "#294A37", "#2B4133")),
        new("amber", "Amber", "Warm sand surfaces with a copper accent.",
            new("#F5EFE5", "#FFFBF4", "#E1D4BF", "#C6B293", "#35291B", "#71604B", "#875219", "#794813", "#F1E1C6", "#ECE2D3"),
            new("#261E16", "#352A1E", "#51412F", "#756044", "#FAF0DF", "#CDBB9E", "#955E22", "#F1C17F", "#523B22", "#463727")),
    ];

    /// <summary>Old settings and unrecognized IDs retain the original appearance.</summary>
    public static AppSkin Find(string? id) => All.FirstOrDefault(s => s.Id == id) ?? All[0];
}
