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
    public StationRowViewModel(RankedStation r, int rank, DistanceUnit units, DateTime nowUtc)
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
        History = r.Station.SnrHistory;
        HistoryTip = HistoryText(r.Station.SnrHistory);
        Refresh(nowUtc);
        CallText = r.CallingMe ? "Answer" : "Call";
    }

    /// <summary>SNR in the station's last few own slots, oldest first, for the sparkline.</summary>
    public IReadOnlyList<int?> History { get; }

    /// <summary>The sparkline in words: "Last 6 slots: -12, -10, missed, -8 dB".</summary>
    public string HistoryTip { get; }

    /// <summary>The sparkline in words.</summary>
    public static string HistoryText(IReadOnlyList<int?> history) =>
        history.Count == 0 ? "No signal history yet"
            : $"Last {history.Count} slots: {string.Join(", ", history.Select(h => h is { } v ? Report.Format(v) : "missed"))} dB";

    /// <summary>"Answer" for a station calling me (the button answers it from Waiting for you), otherwise "Call".</summary>
    public string CallText { get; }

    /// <summary>The ranked station.</summary>
    public RankedStation Source { get; }

    /// <summary>Rank from 1.</summary>
    public int Rank { get; }

    /// <summary>Call.</summary>
    public string Call { get; }

    /// <summary>Place and distance; updated in place when a lookup arrives between re-ranks.</summary>
    [ObservableProperty]
    public partial string Where { get; private set; }

    /// <summary>Shows the latest place for the station (a QRZ lookup may have filled in its city and state).</summary>
    public void UpdatePlace(Core.Stations.Station s, DistanceUnit units) => Where = StationText.Where(s, units);

    /// <summary>Need tag text.</summary>
    public string Why { get; }

    /// <summary>Need tag colour.</summary>
    public TextKind WhyKind { get; }

    /// <summary>True for a bold need tag (new country, band, grid).</summary>
    public bool WhyBold => WhyKind is TextKind.Accent or TextKind.Caution;

    /// <summary>Doing now.</summary>
    public string DoingNow { get; }

    /// <summary>How long since the station was last decoded: "45 seconds ago".</summary>
    [ObservableProperty]
    public partial string LastHeard { get; private set; } = string.Empty;

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

    /// <summary>Updates the age text. Called on every timer tick so a held list does not show a stale age.</summary>
    public void Refresh(DateTime nowUtc) => LastHeard = StationText.Ago(nowUtc - Source.Station.LastHeardUtc);

    /// <summary>Colour for a need tag.</summary>
    public static TextKind KindOf(NeedTag t) => t switch
    {
        NeedTag.NewCountry => TextKind.Caution,
        NeedTag.NewBand or NeedTag.NewGrid => TextKind.Accent,
        NeedTag.Worked => TextKind.Secondary,
        _ => TextKind.Normal,
    };
}
