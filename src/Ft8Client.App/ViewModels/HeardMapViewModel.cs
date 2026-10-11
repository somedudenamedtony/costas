// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// The heard-here map on the Operate view: who I am hearing (and, optionally, who hears me) on a map, with choices for
/// the projection, which stations to show and what the colour means. Also the band activity chart under it.
/// </summary>
public sealed partial class HeardMapViewModel : ObservableObject
{
    /// <summary>How far back a station counts as on the map.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>View choices.</summary>
    public static IReadOnlyList<string> Views { get; } = ["Around me", "World"];

    /// <summary>Show choices.</summary>
    public static IReadOnlyList<string> Shows { get; } = ["Everyone heard", "Needed only", "Calling CQ"];

    /// <summary>Colour choices.</summary>
    public static IReadOnlyList<string> Colours { get; } = ["Need", "Signal", "Last heard"];

    private SessionSnapshot? _snap;
    private DateTime _now;

    /// <summary>Country file, for placing reporters with no grid.</summary>
    public CountryFile? Countries { get; set; }

    /// <summary>Distance units.</summary>
    [ObservableProperty]
    public partial DistanceUnit Units { get; set; } = DistanceUnit.Miles;

    /// <summary>Raised when a choice changes, so it can be saved.</summary>
    public event Action? ChoicesChanged;

    /// <summary>Selected view.</summary>
    [ObservableProperty]
    public partial string View { get; set; } = "Around me";

    /// <summary>Selected stations to show.</summary>
    [ObservableProperty]
    public partial string Show { get; set; } = "Everyone heard";

    /// <summary>What colour means.</summary>
    [ObservableProperty]
    public partial string Colour { get; set; } = "Need";

    /// <summary>Also show stations that heard me.</summary>
    [ObservableProperty]
    public partial bool HearsMe { get; set; } = true;

    /// <summary>Shade night on the world view.</summary>
    [ObservableProperty]
    public partial bool DayNight { get; set; } = true;

    /// <summary>True for the flat world map.</summary>
    public bool World => View == "World";

    /// <summary>Points to draw.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MapPoint> Points { get; set; } = [];

    /// <summary>My position.</summary>
    [ObservableProperty]
    public partial LatLon? Centre { get; set; }

    /// <summary>Now, for the night shading and the activity chart.</summary>
    [ObservableProperty]
    public partial DateTime NowUtc { get; set; }

    /// <summary>"32 stations heard · 7 hear you".</summary>
    [ObservableProperty]
    public partial string Headline { get; set; } = string.Empty;

    /// <summary>Note under the map when it cannot be centred on me.</summary>
    [ObservableProperty]
    public partial string Notice { get; set; } = string.Empty;

    /// <summary>Legend lines.</summary>
    public ObservableCollection<LegendItem> Legend { get; } = [];

    /// <summary>Per-slot activity for the chart.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SlotActivity> Activity { get; set; } = [];

    /// <summary>"Busier than half an hour ago" and similar.</summary>
    [ObservableProperty]
    public partial string ActivityText { get; set; } = string.Empty;

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s, DateTime nowUtc)
    {
        _snap = s;
        _now = nowUtc;
        Rebuild();
    }

    partial void OnViewChanged(string value)
    {
        OnPropertyChanged(nameof(World));
        Changed();
    }

    partial void OnShowChanged(string value) => Changed();

    partial void OnColourChanged(string value) => Changed();

    partial void OnHearsMeChanged(bool value) => Changed();

    partial void OnDayNightChanged(bool value) => Changed();

    private void Changed()
    {
        Rebuild();
        ChoicesChanged?.Invoke();
    }

    private void Rebuild()
    {
        if (_snap is not { } s) return;
        NowUtc = _now;
        Centre = Grid.ToLatLon(s.MyGrid);
        Notice = Centre is null ? "Set your grid in Settings to centre the map on you." : string.Empty;

        var all = s.Rank.Line.Concat(s.Rank.Watching).Concat(s.Rank.Worked);
        var heard = BuildHeard(all, _now, Show, Colour, Units);
        var hears = HearsMe ? BuildHearsMe(s.Reports, Countries, _now) : [];
        // Rings underneath, so a station that hears me and that I hear shows as a dot inside a ring.
        Points = [.. hears, .. heard];
        Headline = $"{heard.Count} station{(heard.Count == 1 ? "" : "s")} heard in the last 15 minutes"
                   + (HearsMe ? $" · {hears.Count} heard you" : string.Empty);

        Legend.Clear();
        foreach (var item in LegendFor(Colour)) Legend.Add(item);
        if (HearsMe) Legend.Add(new LegendItem(TextKind.Success, "Heard you (PSK Reporter)", Ring: true));

        Activity = s.Activity;
        ActivityText = ActivitySentence(s.Activity, _now);
    }

    /// <summary>Dots for stations I heard in the window, filtered and coloured by the choices.</summary>
    public static IReadOnlyList<MapPoint> BuildHeard(IEnumerable<RankedStation> stations, DateTime nowUtc, string show, string colour, DistanceUnit units)
    {
        var list = new List<MapPoint>();
        foreach (var r in stations)
        {
            var st = r.Station;
            var age = nowUtc - st.LastHeardUtc;
            if (age > Window) continue;
            var needed = r.Need.Tag != NeedTag.Worked;
            if (show == "Needed only" && !needed) continue;
            if (show == "Calling CQ" && st.State != StationState.CallingCq) continue;
            if ((Grid.ToLatLon(st.Grid) ?? st.Entity?.Position) is not { } pos) continue;

            var (kind, size, opacity) = colour switch
            {
                "Signal" => (TextKind.Accent, st.LastSnr >= -10 ? 6.0 : st.LastSnr >= -17 ? 4.5 : 3.5, st.LastSnr >= -10 ? 1.0 : st.LastSnr >= -17 ? 0.75 : 0.45),
                "Last heard" => (TextKind.Accent, 4.5, age <= TimeSpan.FromMinutes(1) ? 1.0 : age <= TimeSpan.FromMinutes(5) ? 0.6 : 0.3),
                _ => (needed ? StationRowViewModel.KindOf(r.Need.Tag) : TextKind.Secondary, needed ? 5.0 : 3.5, needed ? 1.0 : 0.55),
            };
            var tip = $"{st.Call} · {StationText.Where(st, units)} · {StationText.Need(r.Need.Tag)} · {Report.Format(st.LastSnr)} · heard {StationText.Ago(age)}";
            list.Add(new MapPoint(pos, st.Call, tip, kind, size, opacity, Ring: false));
        }
        return list;
    }

    /// <summary>Rings for stations that reported hearing me in the window, one per receiver (its best report).</summary>
    public static IReadOnlyList<MapPoint> BuildHearsMe(IEnumerable<ReceptionReport> reports, CountryFile? countries, DateTime nowUtc)
    {
        var list = new List<MapPoint>();
        foreach (var g in reports.Where(r => nowUtc - r.TimeUtc <= Window).GroupBy(r => Callsign.Normalize(r.ReceiverCall)))
        {
            var best = g.MaxBy(r => r.Snr)!;
            if ((Grid.ToLatLon(best.ReceiverGrid) ?? countries?.Lookup(g.Key)?.Position) is not { } pos) continue;
            var tip = $"{g.Key} heard you at {Report.Format(best.Snr)} · {StationText.Ago(nowUtc - g.Max(r => r.TimeUtc))}";
            list.Add(new MapPoint(pos, g.Key, tip, TextKind.Success, 5.5, 0.9, Ring: true));
        }
        return list;
    }

    /// <summary>Legend for a colour choice.</summary>
    public static IReadOnlyList<LegendItem> LegendFor(string colour) => colour switch
    {
        "Signal" =>
        [
            new(TextKind.Accent, "Strong, -10 dB or better"),
            new(TextKind.Accent, "Fair, -11 to -17 dB", Opacity: 0.75),
            new(TextKind.Accent, "Weak, -18 dB or below", Opacity: 0.45),
        ],
        "Last heard" =>
        [
            new(TextKind.Accent, "In the last minute"),
            new(TextKind.Accent, "1 to 5 minutes ago", Opacity: 0.6),
            new(TextKind.Accent, "5 to 15 minutes ago", Opacity: 0.3),
        ],
        _ =>
        [
            new(TextKind.Caution, "New country"),
            new(TextKind.Accent, "New band or new grid"),
            new(TextKind.Normal, "New call"),
            new(TextKind.Secondary, "Already worked", Opacity: 0.55),
        ],
    };

    /// <summary>
    /// One line about the trend: the last 10 minutes against 30 to 40 minutes ago, by average decodes per slot.
    /// Needs at least two slots in each window to say anything.
    /// </summary>
    public static string ActivitySentence(IReadOnlyList<SlotActivity> activity, DateTime nowUtc)
    {
        var recent = activity.Where(a => nowUtc - a.SlotStartUtc <= TimeSpan.FromMinutes(10)).ToList();
        var earlier = activity.Where(a => nowUtc - a.SlotStartUtc is var t && t > TimeSpan.FromMinutes(30) && t <= TimeSpan.FromMinutes(40)).ToList();
        if (recent.Count < 2) return "Band activity appears here as slots are decoded.";
        var now = recent.Average(a => a.Decodes);
        var needed = recent.Average(a => a.Needed);
        var head = $"About {Math.Round(now):0} decodes and {Math.Round(needed):0} needed stations per slot.";
        if (earlier.Count < 2) return head;
        var then = earlier.Average(a => a.Decodes);
        var change = then <= 0 ? (now > 0 ? 1 : 0) : (now - then) / then;
        var trend = change >= 0.25 ? "Busier than half an hour ago." : change <= -0.25 ? "Quieter than half an hour ago." : "About as busy as half an hour ago.";
        return $"{head} {trend}";
    }
}
