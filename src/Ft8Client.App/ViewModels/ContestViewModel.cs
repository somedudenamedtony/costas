// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;
using Ft8Client.Core.Contests;
using Ft8Client.Core.Encoding;
using Ft8Client.Core.Messages;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// The Contest tab, shown only while a contest is picked: score, rate, multipliers, the contest log with a dupe check,
/// and Cabrillo export. Contacts are still worked on Operate (owner decision 2026-11-07).
/// </summary>
public sealed partial class ContestViewModel : ObservableObject
{
    private readonly IOperateCommands _commands;
    private readonly Func<DateTime> _now;
    private ContestSnapshot? _shown;

    /// <summary>Creates the view model.</summary>
    public ContestViewModel(IOperateCommands commands, Func<DateTime> now)
    {
        _commands = commands;
        _now = now;
    }

    /// <summary>True while a contest is picked.</summary>
    [ObservableProperty]
    public partial bool Visible { get; set; }

    /// <summary>The contest's name.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>The CQ modifier, for the tab's badge.</summary>
    [ObservableProperty]
    public partial string Badge { get; set; } = string.Empty;

    /// <summary>"Since Jan 2 18:00 UTC · 6 h in".</summary>
    [ObservableProperty]
    public partial string Since { get; set; } = string.Empty;

    /// <summary>Contacts that count.</summary>
    [ObservableProperty]
    public partial string Contacts { get; set; } = "0";

    /// <summary>Dupes, under the contact count.</summary>
    [ObservableProperty]
    public partial string ContactsDetail { get; set; } = string.Empty;

    /// <summary>Multipliers.</summary>
    [ObservableProperty]
    public partial string Mults { get; set; } = "0";

    /// <summary>What the multipliers are.</summary>
    [ObservableProperty]
    public partial string MultsDetail { get; set; } = string.Empty;

    /// <summary>True when the contest has multipliers.</summary>
    [ObservableProperty]
    public partial bool HasMults { get; set; }

    /// <summary>Claimed score.</summary>
    [ObservableProperty]
    public partial string Score { get; set; } = "0";

    /// <summary>How the score is made.</summary>
    [ObservableProperty]
    public partial string ScoreDetail { get; set; } = string.Empty;

    /// <summary>Contacts in the last hour.</summary>
    [ObservableProperty]
    public partial string Rate { get; set; } = "0";

    /// <summary>Rate over the last 10 minutes.</summary>
    [ObservableProperty]
    public partial string RateDetail { get; set; } = string.Empty;

    /// <summary>The per-band table.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ContestBandRowViewModel> Bands { get; set; } = [];

    /// <summary>The multiplier grid (states, then provinces, for the Roundup; fields for WW Digi).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ContestMultChip> MultChips { get; set; } = [];

    /// <summary>What the grid shows, and the DX countries for the Roundup.</summary>
    [ObservableProperty]
    public partial string MultsNote { get; set; } = string.Empty;

    /// <summary>The contest log, newest first, filtered by the dupe check.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ContestLogRowViewModel> Log { get; set; } = [];

    /// <summary>The note under the log.</summary>
    [ObservableProperty]
    public partial string LogNote { get; set; } = string.Empty;

    /// <summary>A call typed into the dupe check.</summary>
    [ObservableProperty]
    public partial string DupeCheck { get; set; } = string.Empty;

    /// <summary>The dupe check's answer, or empty.</summary>
    [ObservableProperty]
    public partial string DupeAnswer { get; set; } = string.Empty;

    /// <summary>True when the dupe check found the call on this band.</summary>
    [ObservableProperty]
    public partial bool DupeFound { get; set; }

    private IReadOnlyList<ContestLogRowViewModel> _all = [];
    private string _band = string.Empty;

    partial void OnDupeCheckChanged(string value) => Filter();

    /// <summary>Shows <paramref name="c"/>; rebuilds the tables only when the contest log changed.</summary>
    /// <param name="c">The contest, or null when none is picked.</param>
    /// <param name="band">The band now, for the dupe check.</param>
    public void Apply(ContestSnapshot? c, string band)
    {
        Visible = c is not null;
        _band = band;
        if (c is null)
        {
            _shown = null;
            return;
        }
        var now = _now();
        var hours = Math.Max(0, (now - c.SinceUtc).TotalHours);
        Since = $"Since {c.SinceUtc.ToLocalTime():MMM d HH:mm} · {(hours < 1 ? "under an hour" : $"{hours:0} h")} in";
        Rate = c.RatePerHour.ToString(CultureInfo.InvariantCulture);
        RateDetail = $"last 10 min: {c.RateLast10} /h";
        if (_shown is { } old && old.Score.All.Count == c.Score.All.Count && old.Info == c.Info && old.SinceUtc == c.SinceUtc)
        {
            _shown = c;
            Filter();
            return;
        }
        _shown = c;
        var s = c.Score;
        Title = c.Info.Name;
        Badge = c.Info.CqModifier;
        HasMults = s.HasMults;
        Contacts = s.Contacts.ToString(CultureInfo.InvariantCulture);
        ContactsDetail = s.Dupes == 0 ? string.Empty : $"{s.Dupes} dupe{(s.Dupes == 1 ? "" : "s")} not counted";
        Mults = s.Mults.Count.ToString(CultureInfo.InvariantCulture);
        Score = s.Score.ToString(CultureInfo.InvariantCulture);
        ScoreDetail = s.HasMults ? $"{s.Points} points × {s.Mults.Count} mults" : $"{s.Points} points";
        Bands = [.. s.Bands.OrderBy(b => BandOrder(b.Key)).Select(b => new ContestBandRowViewModel(b.Key,
            b.Value.Contacts.ToString(CultureInfo.InvariantCulture), b.Value.Mults.ToString(CultureInfo.InvariantCulture)))];
        BuildMults(c);
        _all = Rows(c);
        LogNote = "Every contest contact also goes in your normal log and to QRZ as usual." + " A station counts once per band."
            + " Field Day and WW Digi score points and bonuses this tab cannot see; check the rules before you submit.";
        Filter();
    }

    /// <summary>Saves the contest log as a Cabrillo file.</summary>
    [RelayCommand]
    private void Export() => _commands.ExportCabrillo();

    private void BuildMults(ContestSnapshot c)
    {
        var worked = c.Score.Mults;
        switch (c.Info.Id)
        {
            case ContestId.RttyRoundup:
                MultChips = [.. ContestTables.UsStates.Order(StringComparer.Ordinal).Append("DC")
                    .Concat(ContestTables.CanadianAreas.Order(StringComparer.Ordinal))
                    .Select(m => new ContestMultChip(m, worked.Contains(m)))];
                var dx = worked.Where(m => m.StartsWith("DX ", StringComparison.Ordinal)).Select(m => m[3..]).ToList();
                var states = worked.Count(m => ContestTables.UsStates.Contains(m) || m == "DC");
                var provinces = worked.Count(m => ContestTables.CanadianAreas.Contains(m));
                MultsDetail = $"{Count(states, "state")}, {Count(provinces, "province")}, {Count(dx.Count, "country", "countries")}";
                MultsNote = dx.Count == 0 ? "DX countries: none yet" : $"DX countries: {dx.Count} ({string.Join(", ", dx)})";
                break;
            case ContestId.WwDigi:
                MultChips = [.. worked.Order(StringComparer.Ordinal).Select(m => new ContestMultChip(m.Replace(":", " ", StringComparison.Ordinal), true))];
                MultsDetail = "grid fields, counted per band";
                MultsNote = worked.Count == 0 ? "No fields worked yet." : "Each field counts once on each band.";
                break;
            default:
                MultChips = [];
                MultsDetail = string.Empty;
                MultsNote = c.Info.Id == ContestId.FieldDay
                    ? "Field Day has no multipliers: each digital contact is 2 points, before power and bonus points."
                    : "ARRL Digi has no multipliers here: points grow with distance.";
                break;
        }
    }

    private static List<ContestLogRowViewModel> Rows(ContestSnapshot c)
    {
        var worked = new HashSet<(string, string)>();
        var mults = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ContestLogRowViewModel>();
        foreach (var q in c.Score.All.OrderBy(q => q.TimeUtc))
        {
            var dupe = !worked.Add((Callsign.Normalize(q.Call), q.Band));
            var mult = dupe ? null : c.Score.MultOf(q.Call, q.Band, q.Received);
            var isNew = mult is not null && mults.Add(mult);
            rows.Add(new ContestLogRowViewModel(q.TimeUtc.ToString("HH:mm", CultureInfo.InvariantCulture), q.Band, q.Call, q.Sent, q.Received,
                dupe ? "Dupe" : isNew ? "New mult" : string.Empty, dupe));
        }
        rows.Reverse();
        return rows;
    }

    private void Filter()
    {
        var q = DupeCheck.Trim().ToUpperInvariant();
        if (q.Length == 0)
        {
            Log = _all;
            DupeAnswer = string.Empty;
            DupeFound = false;
            return;
        }
        Log = [.. _all.Where(r => r.Call.Contains(q, StringComparison.Ordinal))];
        var exact = _all.Where(r => Callsign.EqualsCall(r.Call, q)).ToList();
        var here = exact.FirstOrDefault(r => r.Band == _band);
        DupeFound = here is not null;
        DupeAnswer = exact.Count == 0 ? $"{q}: not worked yet. Go ahead."
            : here is not null ? $"{q}: dupe, already worked on {_band} at {here.Time}."
            : $"{q}: worked on {string.Join(", ", exact.Select(r => r.Band).Distinct())}, not on {_band}. Go ahead.";
    }

    private static string Count(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";

    private static int BandOrder(string band) =>
        int.TryParse(band.TrimEnd('m', 'c'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? -m : 0;
}
