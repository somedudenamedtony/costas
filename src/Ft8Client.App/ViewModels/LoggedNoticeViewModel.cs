// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// The "Logged" notice in the corner of the main window: shown once per logged contact and closed after
/// <see cref="ShowFor"/> with a visible countdown. The pointer resting on it keeps it open; ✕ closes it.
/// It never takes focus, so keys (Esc to halt) keep going to the window.
/// </summary>
public sealed partial class LoggedNoticeViewModel : ObservableObject
{
    /// <summary>How long the notice stays up.</summary>
    public static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(5);

    private readonly Func<DateTime> _utcNow;
    private long _shownId;
    private DateTime _closesAtUtc;
    private bool _hovered;

    /// <summary>Creates the notice. <paramref name="utcNow"/> is the app's (corrected) UTC clock.</summary>
    public LoggedNoticeViewModel(Func<DateTime> utcNow) => _utcNow = utcNow;

    /// <summary>The notice is showing.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>The call logged.</summary>
    [ObservableProperty]
    public partial string Call { get; set; } = string.Empty;

    /// <summary>Need tag beside the call, "New call".</summary>
    [ObservableProperty]
    public partial string Need { get; set; } = string.Empty;

    /// <summary>Need colour.</summary>
    [ObservableProperty]
    public partial TextKind NeedKind { get; set; }

    /// <summary>"Indiana, USA · 20 m FT8".</summary>
    [ObservableProperty]
    public partial string Subline { get; set; } = string.Empty;

    /// <summary>"Sent -12 · Received -08 · 14:30 UTC".</summary>
    [ObservableProperty]
    public partial string Reports { get; set; } = string.Empty;

    /// <summary>"Closes in 4 s", or what happens while the pointer rests on it.</summary>
    [ObservableProperty]
    public partial string Countdown { get; set; } = string.Empty;

    /// <summary>Time left as a fraction, 1 when shown down to 0 when it closes.</summary>
    [ObservableProperty]
    public partial double Remaining { get; set; }

    /// <summary>Shows the notice when the snapshot carries a contact not shown yet. UI thread.</summary>
    public void Apply(LoggedContact? logged)
    {
        if (logged is null || logged.Id == _shownId) return;
        _shownId = logged.Id;
        // A snapshot that arrives late (window busy) still gets the full time from now, not from when it was logged.
        Call = logged.Call;
        Need = StationText.Need(logged.Need);
        NeedKind = StationRowViewModel.KindOf(logged.Need);
        Subline = SublineOf(logged);
        Reports = ReportsOf(logged);
        _closesAtUtc = _utcNow() + ShowFor;
        IsOpen = true;
        Tick();
    }

    /// <summary>Counts down; called every 200 ms by the window.</summary>
    public void Tick()
    {
        if (!IsOpen) return;
        var nowUtc = _utcNow();
        if (_hovered)
        {
            Remaining = 1;
            Countdown = "Stays open while the pointer is here";
            return;
        }
        var left = _closesAtUtc - nowUtc;
        if (left <= TimeSpan.Zero)
        {
            IsOpen = false;
            return;
        }
        Remaining = left / ShowFor;
        Countdown = $"Closes in {(int)Math.Ceiling(left.TotalSeconds)} s";
    }

    /// <summary>The pointer entered or left the notice. Leaving starts the full countdown again.</summary>
    public void Hover(bool over)
    {
        _hovered = over;
        if (!over) _closesAtUtc = _utcNow() + ShowFor;
        Tick();
    }

    /// <summary>Closes the notice now.</summary>
    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        _hovered = false;
    }

    /// <summary>"Indiana, USA · 20 m FT8".</summary>
    public static string SublineOf(LoggedContact c) =>
        string.Join(" · ", new[] { c.Place, $"{BandPlan.Display(c.Band)} {ModeInfo.Name(c.Mode)}" }.Where(x => x.Length > 0));

    /// <summary>"Sent -12 · Received -08 · 14:30 UTC", shared with the phone (which leaves the time off).</summary>
    public static string ReportsOf(LoggedContact c, bool withTime = true) =>
        $"Sent {Report.Format(c.ReportSent)} · Received {(c.ReportReceived is { } r ? Report.Format(r) : ContactPanelViewModel.NotYet)}"
        + (withTime ? $" · {OperateViewModel.Hm(c.LoggedUtc)}" : string.Empty);
}
