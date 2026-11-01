// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.App.ViewModels;

/// <summary>The classic list of decodes.</summary>
public sealed partial class RawDecodesViewModel(Action<string> call) : ObservableObject
{
    /// <summary>Most callsign highlights kept at once (WSJT-X asks senders to stay under 100).</summary>
    public const int MaxHighlights = 200;

    /// <summary>Show choice: every line.</summary>
    public const string ShowAll = "All lines";

    /// <summary>Show choice: CQs only.</summary>
    public const string ShowCq = "CQs";

    /// <summary>Show choice: lines to me and my own transmissions.</summary>
    public const string ShowMine = "To or from me";

    /// <summary>Show choice: senders that would be a new country, band or grid.</summary>
    public const string ShowNeeded = "New country, band or grid";

    private readonly Dictionary<string, WsjtxHighlight> _highlights = new(StringComparer.Ordinal);
    private IReadOnlyList<RawDecodeLine> _all = [];
    private IReadOnlyDictionary<string, RankedStation> _stations = new Dictionary<string, RankedStation>(StringComparer.Ordinal);
    private string? _partner;
    private bool _highlightsChanged;

    // Lines on screen when the operator pressed Clear. Kept by reference: a later line with the same text and
    // slot (the full decode pass after the early one) is a different line and still shows.
    private readonly HashSet<RawDecodeLine> _cleared = new(ReferenceEqualityComparer.Instance);

    /// <summary>Lines, newest slot first, with a blank line between slots.</summary>
    public ObservableCollection<RawLineViewModel> Lines { get; } = [];

    /// <summary>Filter text: callsign or message text.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>Choices for <see cref="Show"/>.</summary>
    public IReadOnlyList<string> ShowChoices { get; } = [ShowAll, ShowCq, ShowMine, ShowNeeded];

    /// <summary>The show choices as chips with counts.</summary>
    public IReadOnlyList<ShowOptionViewModel> ShowOptions { get; } =
    [
        new(ShowAll, "All"), new(ShowCq, "CQs"), new(ShowMine, "To or from me"), new(ShowNeeded, "New for me"),
    ];

    /// <summary>Chip click.</summary>
    [RelayCommand]
    private void PickShow(string choice) => Show = choice;

    /// <summary>Which lines to show.</summary>
    [ObservableProperty]
    public partial string Show { get; set; } = ShowAll;

    /// <summary>True when the filter or the show choice hides some lines.</summary>
    [ObservableProperty]
    public partial bool Filtered { get; private set; }

    /// <summary>"34 of 212 lines" while filtered.</summary>
    [ObservableProperty]
    public partial string Count { get; private set; } = string.Empty;

    /// <summary>True after Clear, until Show earlier lines brings the cleared lines back.</summary>
    [ObservableProperty]
    public partial bool Cleared { get; private set; }

    /// <summary>"Cleared at 174515 UTC" after Clear.</summary>
    [ObservableProperty]
    public partial string ClearedAt { get; private set; } = string.Empty;

    /// <summary>Distance units for the Distance column.</summary>
    public DistanceUnit Units { get; set; } = DistanceUnit.Miles;

    /// <summary>True when the operator scrolled away from the newest lines (the view keeps position and shows Jump to live).</summary>
    [ObservableProperty]
    public partial bool ScrolledAway { get; set; }

    /// <summary>Raised when the view should scroll to the newest line, at the top.</summary>
    public event Action? ScrollToNewest;

    private bool _keyboard;

    /// <summary>Applies new decodes.</summary>
    /// <param name="lines">Every raw line, oldest first.</param>
    /// <param name="rank">The station list, for each sender's need and distance.</param>
    /// <param name="partner">The station I am in a contact with, if any.</param>
    /// <param name="keyboard">True for PSK and RTTY lines, which have no DT.</param>
    public void Apply(IReadOnlyList<RawDecodeLine> lines, RankResult? rank = null, string? partner = null, bool keyboard = false)
    {
        var partnerChanged = !string.Equals(partner, _partner, StringComparison.Ordinal);
        if (!partnerChanged && keyboard == _keyboard && lines.Count == _all.Count && (lines.Count == 0 || ReferenceEquals(lines[^1], _all[^1]))) return;
        _keyboard = keyboard;
        _all = lines;
        PruneCleared();
        if (rank is not null) _stations = ByCall(rank);
        _partner = partner;
        Rebuild();
    }

    // The session drops a mode's oldest lines past its limit; forget those here too. Lines of other modes stay,
    // so switching back to a cleared mode keeps it cleared.
    private void PruneCleared()
    {
        if (_cleared.Count == 0 || _all.Count == 0) return;
        var mode = _all[^1].Mode;
        var live = new HashSet<RawDecodeLine>(_all, ReferenceEqualityComparer.Instance);
        _cleared.RemoveWhere(l => l.Mode == mode && !live.Contains(l));
        if (_cleared.Count == 0) (Cleared, ClearedAt) = (false, string.Empty);
    }

    private static Dictionary<string, RankedStation> ByCall(RankResult rank)
    {
        var map = new Dictionary<string, RankedStation>(StringComparer.Ordinal);
        foreach (var r in rank.Line.Concat(rank.Watching).Concat(rank.Worked).Concat(rank.CallingMe)) map.TryAdd(r.Station.Call, r);
        return map;
    }

    partial void OnFilterChanged(string value) => Rebuild();

    partial void OnShowChanged(string value) => Rebuild();

    /// <summary>Clears the filter text and shows every line.</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        Filter = string.Empty;
        Show = ShowAll;
    }

    /// <summary>
    /// Clears the list: the lines of the current mode received so far are hidden from this page. Nothing is deleted;
    /// the station list, the phone remote and other programs still see every line, and
    /// <see cref="ShowEarlierCommand"/> brings them back.
    /// </summary>
    [RelayCommand]
    private void Clear() => ClearAt(DateTime.UtcNow);

    /// <summary>Clears as <see cref="ClearCommand"/> does, stamping the given time.</summary>
    public void ClearAt(DateTime utcNow)
    {
        foreach (var l in _all) _cleared.Add(l);
        Cleared = _cleared.Count > 0;
        ClearedAt = Cleared ? $"Cleared at {utcNow:HHmmss} UTC" : string.Empty;
        ScrolledAway = false;
        Rebuild();
    }

    /// <summary>Shows the lines hidden by Clear again.</summary>
    [RelayCommand]
    private void ShowEarlier()
    {
        _cleared.Clear();
        Cleared = false;
        ClearedAt = string.Empty;
        Rebuild();
    }

    /// <summary>Right-click, Show only: filters to the line's sender.</summary>
    public void ShowOnly(RawLineViewModel line)
    {
        if (line.Sender is not null) Filter = line.Sender;
    }

    /// <summary>Jump to live.</summary>
    [RelayCommand]
    private void JumpToLive()
    {
        ScrolledAway = false;
        ScrollToNewest?.Invoke();
    }

    /// <summary>Double-click: call the sender.</summary>
    public void Activate(RawLineViewModel line)
    {
        if (line.Sender is not null) call(line.Sender);
    }

    /// <summary>
    /// Colours a callsign as another program asked (Highlight Callsign over UDP), or cancels it. Call
    /// <see cref="ApplyHighlights"/> afterwards; several highlights in a row then cost one rebuild.
    /// </summary>
    public void Highlight(WsjtxHighlight h)
    {
        var call = h.Callsign.Trim().ToUpperInvariant();
        if (call.Length == 0) return;
        if (h.Clears)
        {
            if (!_highlights.Remove(call)) return;
        }
        else
        {
            if (_highlights.Count >= MaxHighlights && !_highlights.ContainsKey(call)) return;
            _highlights[call] = h;
        }
        _highlightsChanged = true;
    }

    /// <summary>Rebuilds the lines if highlights changed since the last rebuild.</summary>
    public void ApplyHighlights()
    {
        if (_highlightsChanged) Rebuild();
    }

    private WsjtxHighlight? HighlightOf(RawDecodeLine l, DateTime newestSlot)
    {
        if (_highlights.Count == 0 || l.Transmitted) return null;
        foreach (var word in l.Decode.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (_highlights.TryGetValue(word.Trim('<', '>'), out var h) && (!h.LastOnly || l.Decode.SlotStartUtc == newestSlot)) return h;
        }
        return null;
    }

    private void Rebuild()
    {
        _highlightsChanged = false;
        var f = Filter.Trim();
        Lines.Clear();
        var newest = _all.Count == 0 ? DateTime.MinValue : _all.Max(x => x.Decode.SlotStartUtc);
        var shown = 0;
        var total = 0;
        var counts = new int[4];
        // Newest slot first; within a slot, lines keep the order they arrived in. Each slot opens with a heading.
        foreach (var group in _all.Where(l => !_cleared.Contains(l)).GroupBy(l => GroupOf(l.Decode.SlotStartUtc)).OrderByDescending(g => g.Key))
        {
            var slotLines = group.ToList();
            var rows = new List<RawLineViewModel>();
            foreach (var l in slotLines)
            {
                total++;
                var sender = l.Message.SenderCall is { } c && _stations.TryGetValue(c, out var st) ? st : null;
                if (f.Length > 0 && !l.Decode.Text.Contains(f, StringComparison.OrdinalIgnoreCase)
                    && !(l.Country?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)) continue;
                for (var i = 0; i < ShowChoices.Count; i++) if (Shows(ShowChoices[i], l, sender)) counts[i]++;
                if (!Shows(Show, l, sender)) continue;
                rows.Add(new RawLineViewModel(l, highlight: HighlightOf(l, newest), sender: sender, units: Units, partner: IsPartner(l), keyboard: _keyboard));
            }
            if (rows.Count == 0) continue;
            Lines.Add(RawLineViewModel.SlotHeading(group.Key, slotLines, _keyboard));
            foreach (var r in rows) Lines.Add(r);
            shown += rows.Count;
        }
        for (var i = 0; i < ShowOptions.Count; i++) ShowOptions[i].Update(counts[i], ShowChoices[i] == Show);
        Filtered = f.Length > 0 || Show != ShowAll;
        Count = Filtered ? $"{shown:N0} of {total:N0} lines" : string.Empty;
        if (!ScrolledAway) ScrollToNewest?.Invoke();
    }

    // FT8 and FT4 group by slot. PSK and RTTY lines carry their own times, so they group by minute.
    private DateTime GroupOf(DateTime utc) => _keyboard ? new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, utc.Kind) : utc;

    private static bool Shows(string show, RawDecodeLine l, RankedStation? sender) => show switch
    {
        ShowCq => l.IsCq && !l.Transmitted,
        ShowMine => l.ToMe || l.Transmitted,
        ShowNeeded => !l.Transmitted && sender?.Need.Tag is NeedTag.NewCountry or NeedTag.NewBand or NeedTag.NewGrid,
        _ => true,
    };

    // A line from the contact partner, or my own transmission to them.
    private bool IsPartner(RawDecodeLine l) =>
        _partner is { } p && (string.Equals(l.Message.SenderCall, p, StringComparison.Ordinal) || (l.Transmitted && l.Message.IsAddressedTo(p)));
}
