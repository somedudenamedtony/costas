// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Media;

namespace Ft8Client.App.Services;

/// <summary>A named desktop skin. Classic leaves Fluent's system accent in place.</summary>
public sealed record AppSkin(string Id, string Name, string Description, SkinPalette Light, SkinPalette Dark, string? StyleResource = null)
{
    public CornerRadius PreviewCornerRadius => Id switch { "radio" => new(3), "retro" => new(2), "vintage" => new(14), "sdr" => new(0), _ => new(4) };
    public bool HasSegmentedDisplay => Id == "retro";
    public bool HasPlainPreview => !HasSegmentedDisplay;
    public FontFamily PreviewFont => Id == "vintage" ? new("Georgia, Liberation Serif, DejaVu Serif") :
        StyleResource is not null ? new("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace") : new("Segoe UI, DejaVu Sans");
    public string PreviewTitle => StyleResource is null ? "Next in line" : Id == "vintage" ? "Receiver dial" : "DIAL FREQUENCY";

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
        new("radio", "Modern Radio", "A transceiver faceplate: recessed digital display, machined panels, illuminated keys and a dial button. Includes a live slot meter.",
            new("#D4DCE2", "#EEF2F5", "#8A9AA8", "#718594", "#152B3A", "#445E70", "#126380", "#105774", "#C4E1EC", "#C2CED7"),
            new("#151C22", "#222E37", "#52616D", "#6B7D89", "#EBF5FB", "#B2C6D3", "#126780", "#70DAF6", "#1B485B", "#35454F"),
            "RadioFace.axaml"),
        new("retro", "Classic Transceiver", "An old-school gray and black transceiver with lime green seven-segment LCD digits, a recessed display, tactile keys and a black tuning dial.",
            new("#CDCFCE", "#E2E4E2", "#838783", "#696F69", "#1B201B", "#505950", "#42631B", "#33520D", "#D3DEC2", "#B9BEBB"),
            new("#181A19", "#2B2E2C", "#656D67", "#858F87", "#EDF2EC", "#B7C0B9", "#446A18", "#B5E75A", "#3B482D", "#404841"),
            "ClassicTransceiver.axaml"),
        new("vintage", "Vintage Receiver", "A classic receiver: a warm illuminated dial, a sweeping slot needle, rounded knobs, brass trim and serif panel labels.",
            new("#E2D5B8", "#F7EED8", "#AA8956", "#907044", "#3E3020", "#69553B", "#79501E", "#6C4416", "#EAD5A9", "#DBCBA8"),
            new("#2B251D", "#3B3327", "#8E744D", "#AD8E5B", "#F7ECD2", "#D1BD94", "#825A28", "#F2CB7E", "#5A4629", "#594B37"),
            "VintageReceiver.axaml"),
        new("sdr", "SDR Console", "A signal workstation: square modules, monospace readouts, a segmented slot strip and a precise instrument grid.",
            new("#E4EDF1", "#F7FBFC", "#A3BBC8", "#7895A7", "#142D3C", "#486777", "#006D82", "#005E72", "#C6E8EF", "#D1E0E7"),
            new("#0B141D", "#122331", "#36546A", "#58788F", "#E3F3FF", "#A1BED3", "#12657D", "#67DEEE", "#153D51", "#233C4F"),
            "SdrConsole.axaml"),
    ];

    /// <summary>Old settings and unrecognized IDs retain the original appearance.</summary>
    public static AppSkin Find(string? id) => All.FirstOrDefault(s => s.Id == id) ?? All[0];
}
