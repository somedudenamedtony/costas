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
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.App.ViewModels;

/// <summary>The classic list of decodes.</summary>
public sealed partial class RawDecodesViewModel(Action<string> call) : ObservableObject
{
    /// <summary>Most callsign highlights kept at once (WSJT-X asks senders to stay under 100).</summary>
    public const int MaxHighlights = 200;

    private readonly Dictionary<string, WsjtxHighlight> _highlights = new(StringComparer.Ordinal);
    private IReadOnlyList<RawDecodeLine> _all = [];
    private bool _highlightsChanged;

    /// <summary>Lines, newest slot first, with a blank line between slots.</summary>
    public ObservableCollection<RawLineViewModel> Lines { get; } = [];

    /// <summary>Filter text: callsign or message text.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>True when the operator scrolled away from the newest lines (the view keeps position and shows Jump to live).</summary>
    [ObservableProperty]
    public partial bool ScrolledAway { get; set; }

    /// <summary>Raised when the view should scroll to the newest line, at the top.</summary>
    public event Action? ScrollToNewest;

    /// <summary>Applies new decodes.</summary>
    public void Apply(IReadOnlyList<RawDecodeLine> lines)
    {
        if (lines.Count == _all.Count && (lines.Count == 0 || ReferenceEquals(lines[^1], _all[^1]))) return;
        _all = lines;
        Rebuild();
    }

    partial void OnFilterChanged(string value) => Rebuild();

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
        DateTime? slot = null;
        // Newest slot first; within a slot, lines keep the order they arrived in.
        foreach (var l in _all.OrderByDescending(x => x.Decode.SlotStartUtc))
        {
            if (f.Length > 0 && !l.Decode.Text.Contains(f, StringComparison.OrdinalIgnoreCase)) continue;
            if (slot is not null && slot != l.Decode.SlotStartUtc) Lines.Add(RawLineViewModel.Blank);
            slot = l.Decode.SlotStartUtc;
            Lines.Add(new RawLineViewModel(l, highlight: HighlightOf(l, newest)));
        }
        if (!ScrolledAway) ScrollToNewest?.Invoke();
    }
}
