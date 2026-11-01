// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Neighbours;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// One band in the band menu and Reach’s activity list: its name and, when PSK Reporter shows stations near me on it, how busy it is, where they
/// are heard and which of those countries I need there. The rows persist; <see cref="Apply"/> updates them in place.
/// </summary>
public sealed partial class BandChoiceViewModel : ObservableObject
{
    /// <summary>Widest activity bar, in pixels.</summary>
    public const double MaxBar = 60;

    /// <summary>Creates a row.</summary>
    /// <param name="band">ADIF band ("20m").</param>
    public BandChoiceViewModel(string band)
    {
        Band = band.ToLowerInvariant();
        Name = BandPlan.Display(Band);
    }

    /// <summary>ADIF band, lower case.</summary>
    public string Band { get; }

    /// <summary>Shown name ("20 m"); also the selected value.</summary>
    public string Name { get; }

    /// <summary>True when the row has activity to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InGrid))]
    public partial bool HasActivity { get; set; }

    /// <summary>
    /// "1,240 spots" ("~1,240 spots" when estimated from samples), "No spots" when the feed is live but nobody near me is
    /// heard here, or why this band has nothing to show yet.
    /// </summary>
    [ObservableProperty]
    public partial string SpotsText { get; set; } = string.Empty;

    /// <summary>The spot count alone for Reach's grid: "1,240", "~1,240", or empty when there are none.</summary>
    [ObservableProperty]
    public partial string SpotsCount { get; set; } = string.Empty;

    /// <summary>True when the feed is live but nobody near me is heard on this band; Reach folds these into one line.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InGrid))]
    public partial bool IsQuiet { get; set; }

    /// <summary>True when the band has a row in Reach's activity grid: it has numbers, or a reason it has none yet.</summary>
    public bool InGrid => HasActivity && !IsQuiet;

    /// <summary>True when this band has a reason in place of numbers ("Checking soon").</summary>
    [ObservableProperty]
    public partial bool NotFollowed { get; set; }

    /// <summary>True when this is the band the radio is on.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>"Brazil +2" for Reach's narrow Need column, or empty.</summary>
    [ObservableProperty]
    public partial string NeedShort { get; set; } = string.Empty;

    /// <summary>Activity bar width in pixels, scaled to the busiest band.</summary>
    [ObservableProperty]
    public partial double BarWidth { get; set; }

    /// <summary>One chip per continent, in a fixed order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ContinentChip> Continents { get; set; } = [];

    /// <summary>"You need Brazil +2", or empty.</summary>
    [ObservableProperty]
    public partial string NeedText { get; set; } = string.Empty;

    /// <summary>Every needed country (tooltip of <see cref="NeedText"/>).</summary>
    [ObservableProperty]
    public partial string NeedTip { get; set; } = string.Empty;

    /// <summary>Where the numbers come from (tooltip).</summary>
    [ObservableProperty]
    public partial string Tip { get; set; } = string.Empty;

    /// <summary>Hunt list stations heard on this band in the last 15 minutes: "VK9XA (Christmas Island)", or empty.</summary>
    [ObservableProperty]
    public partial string HuntText { get; set; } = string.Empty;

    /// <summary>Each sighting in full (tooltip of <see cref="HuntText"/>).</summary>
    [ObservableProperty]
    public partial string HuntTip { get; set; } = string.Empty;

    /// <summary>True when a Hunt list station was heard on this band recently.</summary>
    [ObservableProperty]
    public partial bool HasHunt { get; set; }

    /// <summary>Sets the Hunt list stations heard on this band.</summary>
    /// <param name="names">Short names, newest first.</param>
    /// <param name="tip">The full sentences.</param>
    public void ApplyHunt(IReadOnlyList<string> names, string tip)
    {
        HasHunt = names.Count > 0;
        HuntText = names.Count switch
        {
            0 => string.Empty,
            1 => "Hunting: " + names[0],
            _ => $"Hunting: {names[0]} +{names.Count - 1}",
        };
        HuntTip = tip;
    }

    /// <summary>Updates the row.</summary>
    /// <param name="opening">This band's opening, or null when nothing near me is heard on it.</param>
    /// <param name="maxSpots">Spots on the busiest band, for the bar.</param>
    /// <param name="live">True when the feed is delivering reports of nearby stations (some band has spots).</param>
    /// <param name="units">For the tooltip's radius.</param>
    public void Apply(BandOpening? opening, int maxSpots, bool live, DistanceUnit units)
    {
        HasActivity = live;
        NotFollowed = false;
        if (!live)
        {
            IsQuiet = false;
            SpotsCount = string.Empty;
            NeedShort = string.Empty;
            SpotsText = string.Empty;
            BarWidth = 0;
            Continents = [];
            NeedText = string.Empty;
            NeedTip = string.Empty;
            Tip = string.Empty;
            return;
        }
        var spots = opening?.Spots ?? 0;
        var estimated = opening?.Estimated == true;
        SpotsText = spots == 0 ? "No spots" : BandOpeningText.Spots(spots, estimated);
        IsQuiet = spots == 0;
        SpotsCount = spots == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{(estimated ? "~" : "")}{spots:N0}");
        BarWidth = maxSpots <= 0 ? 0 : Math.Max(spots > 0 ? 2 : 0, MaxBar * spots / maxSpots);
        Continents = [.. BandOpenings.Continents.Select(c =>
        {
            var n = opening?.SpotsByContinent.GetValueOrDefault(c) ?? 0;
            return new ContinentChip(c, BandOpenings.Level(n), $"{c} · {BandOpeningText.Spots(n, estimated)}", BandOpeningText.Compact(n));
        })];
        NeedText = opening is null ? string.Empty : BandOpeningText.Need(opening.NeededEntities);
        NeedShort = opening is null ? string.Empty : BandOpeningText.NeedShort(opening.NeededEntities);
        NeedTip = opening is null ? string.Empty : BandOpeningText.NeedTip(opening.NeededEntities);
        Tip = BandOpeningText.Basis(units, opening?.SampledFor);
    }

    /// <summary>Says why this band has no numbers yet, in place of them.</summary>
    /// <param name="text">"Checking soon".</param>
    /// <param name="tip">The longer reason.</param>
    public void ApplyNotFollowed(string text, string tip)
    {
        HasActivity = true;
        NotFollowed = true;
        IsQuiet = false;
        SpotsCount = string.Empty;
        NeedShort = string.Empty;
        SpotsText = text;
        BarWidth = 0;
        Continents = [];
        NeedText = string.Empty;
        NeedTip = string.Empty;
        Tip = tip;
    }
}
