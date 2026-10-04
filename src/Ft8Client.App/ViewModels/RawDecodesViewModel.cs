// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;

namespace Ft8Client.App.ViewModels;

/// <summary>The classic list of decodes.</summary>
public sealed partial class RawDecodesViewModel(Action<string> call) : ObservableObject
{
    private IReadOnlyList<RawDecodeLine> _all = [];

    /// <summary>Lines, oldest first, with a blank line between slots.</summary>
    public ObservableCollection<RawLineViewModel> Lines { get; } = [];

    /// <summary>Filter text: callsign or message text.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>True when the operator scrolled up (the view keeps position and shows Jump to live).</summary>
    [ObservableProperty]
    public partial bool ScrolledUp { get; set; }

    /// <summary>Raised when the view should scroll to the newest line.</summary>
    public event Action? ScrollToEnd;

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
        ScrolledUp = false;
        ScrollToEnd?.Invoke();
    }

    /// <summary>Double-click: call the sender.</summary>
    public void Activate(RawLineViewModel line)
    {
        if (line.Sender is not null) call(line.Sender);
    }

    private void Rebuild()
    {
        var f = Filter.Trim();
        Lines.Clear();
        DateTime? slot = null;
        foreach (var l in _all)
        {
            if (f.Length > 0 && !l.Decode.Text.Contains(f, StringComparison.OrdinalIgnoreCase)) continue;
            if (slot is not null && slot != l.Decode.SlotStartUtc) Lines.Add(RawLineViewModel.Blank);
            slot = l.Decode.SlotStartUtc;
            Lines.Add(new RawLineViewModel(l));
        }
        if (!ScrolledUp) ScrollToEnd?.Invoke();
    }
}
