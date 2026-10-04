// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>One row of Next in line or Already worked.</summary>
public sealed partial class StationRowViewModel : ObservableObject
{
    /// <summary>Builds a row.</summary>
    public StationRowViewModel(RankedStation r, int rank, DistanceUnit units)
    {
        Source = r;
        Rank = rank;
        Call = r.Station.Call;
        Where = StationText.Where(r.Station, units);
        Why = StationText.Need(r.Need.Tag);
        WhyKind = KindOf(r.Need.Tag);
        DoingNow = StationText.DoingNow(r.Station);
        HearsYou = StationText.Hears(r.Hears);
        Db = Report.Format(r.Station.LastSnr);
        Chance = StationText.Chance(r.Chance);
        ChanceBold = r.Chance == Core.Ranking.Chance.Good;
    }

    /// <summary>The ranked station.</summary>
    public RankedStation Source { get; }

    /// <summary>Rank from 1.</summary>
    public int Rank { get; }

    /// <summary>Call.</summary>
    public string Call { get; }

    /// <summary>Place and distance.</summary>
    public string Where { get; }

    /// <summary>Need tag text.</summary>
    public string Why { get; }

    /// <summary>Need tag colour.</summary>
    public TextKind WhyKind { get; }

    /// <summary>True for a bold need tag (new country, band, grid).</summary>
    public bool WhyBold => WhyKind is TextKind.Accent or TextKind.Caution;

    /// <summary>Doing now.</summary>
    public string DoingNow { get; }

    /// <summary>Hears you.</summary>
    public string HearsYou { get; }

    /// <summary>SNR at my station.</summary>
    public string Db { get; }

    /// <summary>Chance.</summary>
    public string Chance { get; }

    /// <summary>Good is bold.</summary>
    public bool ChanceBold { get; }

    /// <summary>Selected row.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Colour for a need tag.</summary>
    public static TextKind KindOf(NeedTag t) => t switch
    {
        NeedTag.NewCountry => TextKind.Caution,
        NeedTag.NewBand or NeedTag.NewGrid => TextKind.Accent,
        NeedTag.Worked => TextKind.Secondary,
        _ => TextKind.Normal,
    };
}
