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

/// <summary>One row of Repeats: a station heard now that is already in the log on this band and mode.</summary>
public sealed partial class RepeatRowViewModel : ObservableObject
{
    /// <summary>Builds a row.</summary>
    /// <param name="r">The ranked station (tier 5, worked before).</param>
    /// <param name="last">The most recent log contact with this call, if the log has it.</param>
    /// <param name="contacts">How many log contacts there are with this call.</param>
    /// <param name="units">Distance units.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="rank">Position in Repeats, from 1.</param>
    public RepeatRowViewModel(RankedStation r, LogContact? last, int contacts, DistanceUnit units, DateTime nowUtc, int rank = 1)
    {
        Source = r;
        Rank = rank;
        Why = StationText.Need(r.Need.Tag);
        HearsYou = StationText.Hears(r.Hears);
        Chance = StationText.Chance(r.Chance);
        ChanceBold = r.Chance == Core.Ranking.Chance.Good;
        Call = r.Station.Call;
        Where = StationText.Where(r.Station, units);
        CanCall = r.Callable;
        DoingNow = r.Callable || r.Reason is null ? StationText.DoingNow(r.Station) : StationText.Watch(r.Reason, nowUtc);
        Db = Report.Format(r.Station.LastSnr);
        History = r.Station.SnrHistory;
        HistoryTip = StationRowViewModel.HistoryText(r.Station.SnrHistory);
        LastWorked = StationText.LastWorked(last, nowUtc);
        LastWorkedTip = last is null ? string.Empty : StationRowViewModel.LastWorkedText(last, contacts);
        CallText = r.CallingMe ? "Answer" : "Call";
        _heardEndUtc = r.Station.LastHeardEndUtc;
        Refresh(nowUtc);
    }

    /// <summary>The ranked station.</summary>
    public RankedStation Source { get; }

    /// <summary>Position in Repeats, from 1.</summary>
    public int Rank { get; }

    /// <summary>Call.</summary>
    public string Call { get; }

    /// <summary>Need tag text: "Worked before".</summary>
    public string Why { get; }

    /// <summary>Hears you, as in Next in line.</summary>
    public string HearsYou { get; }

    /// <summary>Chance, as in Next in line.</summary>
    public string Chance { get; }

    /// <summary>Good is bold.</summary>
    public bool ChanceBold { get; }

    /// <summary>Place and distance; updated in place when a lookup arrives between re-orders.</summary>
    [ObservableProperty]
    public partial string Where { get; private set; }

    /// <summary>Shows the latest place for the station (a QRZ lookup may have filled in its city and state).</summary>
    public void UpdatePlace(Core.Stations.Station s, DistanceUnit units)
    {
        Where = StationText.Where(s, units);
        // A held list keeps its rows, but the age counts from the station's newest decode.
        if (s.LastHeardEndUtc > _heardEndUtc) _heardEndUtc = s.LastHeardEndUtc;
    }

    private DateTime _heardEndUtc;

    /// <summary>What the station is doing, or why it cannot be called right now.</summary>
    public string DoingNow { get; }

    /// <summary>
    /// True when the station is calling CQ or calling me now. When false, Doing now shows why in grey; the Call button
    /// stays enabled so the operator can still call them (owner request 2026-10-12), except that a station in a contact
    /// with someone else is not called: the session ends that call at once and the contact card says why.
    /// </summary>
    public bool CanCall { get; }

    /// <summary>SNR at my station.</summary>
    public string Db { get; }

    /// <summary>SNR in the station's last few own slots, oldest first, for the sparkline.</summary>
    public IReadOnlyList<int?> History { get; }

    /// <summary>The sparkline in words.</summary>
    public string HistoryTip { get; }

    /// <summary>"Today · 20 m", "2026-10-03 · 40 m".</summary>
    public string LastWorked { get; }

    /// <summary>Tooltip for <see cref="LastWorked"/>: contact count, exact time, band, mode and confirmation.</summary>
    public string LastWorkedTip { get; }

    /// <summary>"Answer" for a station calling me, otherwise "Call".</summary>
    public string CallText { get; }

    /// <summary>How long since the station was last decoded: "45 seconds ago".</summary>
    [ObservableProperty]
    public partial string LastHeard { get; private set; } = string.Empty;

    /// <summary>Updates the age text on every timer tick.</summary>
    public void Refresh(DateTime nowUtc) => LastHeard = StationText.Ago(nowUtc - _heardEndUtc);
}
