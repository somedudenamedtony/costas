// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.App.Services;
using Ft8Client.Core.Awards;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Neighbours;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// The Awards view: DXCC, 5-band DXCC, WAS, WAZ and VUCC progress from the log, and the countries still needed
/// with when the operator has heard each one.
/// </summary>
public sealed partial class AwardsViewModel : ObservableObject
{
    /// <summary>The scope that means "not worked on any band".</summary>
    public const string AnyBand = "Any band";

    private readonly Func<LogIndex> _log;
    private readonly CountryFile _countries;
    private readonly HeardHistoryService _history;
    private readonly Func<DateTime> _now;
    private AwardSummary? _summary;
    private bool _filling;

    /// <summary>Creates the view model.</summary>
    /// <param name="log">The current log index.</param>
    /// <param name="countries">The country file.</param>
    /// <param name="history">Receive history from the decode journal.</param>
    /// <param name="now">UTC now.</param>
    /// <param name="dispatch">Runs an action on the UI thread.</param>
    public AwardsViewModel(Func<LogIndex> log, CountryFile countries, HeardHistoryService history, Func<DateTime> now, Action<Action> dispatch)
    {
        _log = log;
        _countries = countries;
        _history = history;
        _now = now;
        _history.Changed += () => dispatch(FillNeeded);
    }

    /// <summary>Mode choices: "All digital" (the default; PSK and RTTY count beside FT8 and FT4), "All modes", or one mode.</summary>
    public IReadOnlyList<string> ModeChoices { get; } = ModeGroups.Choices;

    /// <summary>The chosen mode; the cards and the needed list count only its contacts.</summary>
    [ObservableProperty]
    public partial string SelectedMode { get; set; } = ModeGroups.AllDigital;

    partial void OnSelectedModeChanged(string value) => Refresh();

    /// <summary>DXCC, WAS, WAZ and VUCC cards.</summary>
    public ObservableCollection<AwardCardViewModel> Cards { get; } = [];

    /// <summary>DXCC per band.</summary>
    public ObservableCollection<AwardBandRowViewModel> Bands { get; } = [];

    /// <summary>"2 of 5 bands have 100 countries confirmed (80, 40, 20, 15 and 10 m)."</summary>
    [ObservableProperty]
    public partial string FiveBandText { get; set; } = string.Empty;

    /// <summary>Scope choices for the needed list: "Any band", then each band in the table.</summary>
    public ObservableCollection<string> Scopes { get; } = [AnyBand];

    /// <summary>The chosen scope.</summary>
    [ObservableProperty]
    public partial string SelectedScope { get; set; } = AnyBand;

    /// <summary>Also list countries worked but not confirmed.</summary>
    [ObservableProperty]
    public partial bool IncludeUnconfirmed { get; set; }

    /// <summary>Countries still needed, the ones you hear first.</summary>
    public ObservableCollection<NeededCountryViewModel> Needed { get; } = [];

    /// <summary>"152 countries not worked on any band".</summary>
    [ObservableProperty]
    public partial string NeededText { get; set; } = string.Empty;

    /// <summary>Where the "when to look" hints come from, or that the journal is being read.</summary>
    [ObservableProperty]
    public partial string HistoryText { get; set; } = string.Empty;

    /// <summary>Shown when the log is empty.</summary>
    [ObservableProperty]
    public partial bool LogEmpty { get; set; }

    /// <summary>Recounts from the log, and re-reads the journal in the background when the cached history is old.</summary>
    public void Refresh()
    {
        var index = _log();
        LogEmpty = index.IsEmpty;
        var mode = SelectedMode;
        _summary = AwardBuilder.Build(Snapshot(index).Where(c => ModeGroups.Matches(mode, c.ModeKey)), _countries);
        var s = _summary;

        Cards.Clear();
        Cards.Add(Card(s.Dxcc, "Countries, any band", "countries", listMissing: false));
        Cards.Add(Card(s.Was, "Worked All States: the 50 US states", "states", listMissing: true));
        Cards.Add(Card(s.Waz, "Worked All Zones: the 40 CQ zones, from each call's prefix", "zones", listMissing: true));
        if (s.Vucc is { } v) Cards.Add(Card(v, "Grid squares on 6 m", "grids", listMissing: false));

        Bands.Clear();
        foreach (var b in s.DxccByBand)
        {
            Bands.Add(new AwardBandRowViewModel(BandPlan.Display(b.Band!), N(b.Worked), N(b.Confirmed), Share(b.Confirmed, b.Goal),
                AwardBuilder.FiveBands.Contains(b.Band!) ? "5-band" : string.Empty));
        }
        FiveBandText = $"5-band DXCC: {s.FiveBandConfirmed} of 5 bands have {AwardBuilder.DxccGoal} countries confirmed"
            + (s.FiveBandWorked > s.FiveBandConfirmed ? $", {s.FiveBandWorked} have {AwardBuilder.DxccGoal} worked." : ".");

        _filling = true;
        var keep = SelectedScope;
        Scopes.Clear();
        Scopes.Add(AnyBand);
        foreach (var b in s.DxccByBand) Scopes.Add(BandPlan.Display(b.Band!));
        SelectedScope = Scopes.Contains(keep) ? keep : AnyBand;
        _filling = false;

        FillNeeded();
        _ = _history.RefreshIfOlderThan(HeardHistoryService.MaxAge);
    }

    partial void OnSelectedScopeChanged(string value)
    {
        if (!_filling) FillNeeded();
    }

    partial void OnIncludeUnconfirmedChanged(bool value) => FillNeeded();

    /// <summary>Fills the needed list for the scope, the countries heard most often first. UI thread.</summary>
    public void FillNeeded()
    {
        HistoryText = HistoryLine();
        if (_summary is null) return;
        var scope = SelectedScope == AnyBand ? null : SelectedScope.Replace(" ", string.Empty, StringComparison.Ordinal);
        var p = scope is null ? _summary.Dxcc : _summary.DxccByBand.FirstOrDefault(b => b.Band == scope) ?? _summary.Dxcc;
        var where = scope is null ? "on any band" : $"on {BandPlan.Display(scope)}";
        NeededText = $"{p.Missing.Count} {(p.Missing.Count == 1 ? "country" : "countries")} not worked {where}"
            + (p.Unconfirmed.Count > 0 ? $" · {p.Unconfirmed.Count} worked, not confirmed" : string.Empty);

        var items = p.Missing.Select(i => (Item: i, Status: "not worked"))
            .Concat(IncludeUnconfirmed ? p.Unconfirmed.Select(i => (Item: i, Status: "worked, not confirmed")) : []);
        var rows = items.Select(x =>
            {
                var heard = _history.Current?.For(x.Item.Key);
                var continent = _countries.FindByKey(x.Item.Key)?.Continent;
                var detail = (continent is null ? string.Empty : NeighbourText.Continent(continent) + " · ") + x.Status;
                var when = _history.Describe(x.Item.Key) ?? "Not heard in your decodes yet.";
                return (Times: heard?.Times ?? 0, Row: new NeededCountryViewModel(x.Item.Name, detail, when, heard is not null));
            })
            .OrderByDescending(r => r.Times).ThenBy(r => r.Row.Name, StringComparer.Ordinal)
            .Select(r => r.Row);
        Needed.Clear();
        foreach (var r in rows) Needed.Add(r);
    }

    // The session adds a just-logged contact to the index from its own thread; copy again if that happens mid-copy.
    private static List<LogContact> Snapshot(LogIndex index)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return index.Contacts.ToList();
            }
            catch (InvalidOperationException) when (attempt < 3)
            {
            }
        }
    }

    private string HistoryLine()
    {
        if (_history.Reading) return "Reading your decode journal for when you hear each country…";
        if (_history.Error is { } e) return e;
        if (_history.Current is not { } h) return string.Empty;
        if (h.Countries == 0) return "Your decode journal has no decodes yet, so there is nothing to say about when you hear each country.";
        var at = _history.ReadUtc is { } t ? t.ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC" : string.Empty;
        return $"When you hear each country comes from {N((int)Math.Min(int.MaxValue, h.Decodes))} decodes in your journal, read at {at}. "
            + "A time is an hour in which you decoded that country on one band.";
    }

    private static AwardCardViewModel Card(AwardProgress p, string about, string noun, bool listMissing)
    {
        var headline = $"{N(p.Worked)} worked · {N(p.Confirmed)} confirmed";
        var goal = $"{p.Goal} confirmed for the award" + (p.Total is { } t ? $" · {t} in all" : string.Empty) + (p.Reached ? " · reached" : string.Empty);
        string missing = string.Empty, tip = string.Empty, unconfirmed = string.Empty;
        if (listMissing && p.Total is not null)
        {
            if (p.Missing.Count > 0)
            {
                missing = $"Still needed ({p.Missing.Count}): " + string.Join(", ", p.Missing.Select(ShortName));
                tip = string.Join("\n", p.Missing.Select(i => i.Name));
            }
            else missing = $"Every one of the {noun} worked.";
            if (p.Unconfirmed.Count > 0) unconfirmed = "Worked, not confirmed yet: " + string.Join(", ", p.Unconfirmed.Select(ShortName));
        }
        return new AwardCardViewModel(p.Title, about, headline, goal, Share(p.Confirmed, p.Goal), Share(p.Worked, p.Goal), missing, tip, unconfirmed);
    }

    // Zones are listed by number on the card ("23"); their full names are in the tooltip.
    private static string ShortName(AwardItem i) => i.Name.StartsWith("Zone ", StringComparison.Ordinal) ? i.Key : i.Name;

    private static double Share(int n, int goal) => goal <= 0 ? 0 : Math.Min(1.0, (double)n / goal);

    private static string N(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
}
