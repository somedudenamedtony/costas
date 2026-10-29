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
/// <see cref="ShowFor"/> with a visible countdown. The pointer resting on it keeps it open; ✕ closes it. Clicking it
/// opens an editor for the contact. The contact is held back from QRZ, eQSL, LoTW and UDP until the notice closes
/// (or the edit is saved or cancelled), so a correction goes out instead of the original. Until clicked it never takes
/// focus, so keys (Esc to halt) keep going to the window.
/// </summary>
public sealed partial class LoggedNoticeViewModel : ObservableObject
{
    /// <summary>How long the notice stays up.</summary>
    public static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(5);

    private readonly Func<DateTime> _utcNow;
    private readonly ILoggedContactStore? _store;
    private long _shownId;
    private long? _qsoId;
    private DateTime _closesAtUtc;
    private bool _hovered;
    private LoggedContact? _waiting;

    /// <summary>Creates the notice. <paramref name="utcNow"/> is the app's (corrected) UTC clock; without a store it cannot edit.</summary>
    public LoggedNoticeViewModel(Func<DateTime> utcNow, ILoggedContactStore? store = null)
    {
        _utcNow = utcNow;
        _store = store;
    }

    /// <summary>The notice is showing.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>The editor is open in place of the summary.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSummary), nameof(Title))]
    public partial bool IsEditing { get; set; }

    /// <summary>The summary and countdown show (not editing).</summary>
    public bool ShowSummary => !IsEditing;

    /// <summary>"Logged", or "Edit" while the editor is open.</summary>
    public string Title => IsEditing ? "Edit" : "Logged";

    /// <summary>The contact is in the log, so clicking the notice can edit it.</summary>
    [ObservableProperty]
    public partial bool CanEdit { get; set; }

    /// <summary>The fields being edited, or null.</summary>
    [ObservableProperty]
    public partial QsoEditViewModel? Editing { get; set; }

    /// <summary>Why Save was refused, or empty.</summary>
    [ObservableProperty]
    public partial string EditError { get; set; } = string.Empty;

    /// <summary>The call logged.</summary>
    [ObservableProperty]
    public partial string Call { get; set; } = string.Empty;

    /// <summary>Need tag beside the call, "New call".</summary>
    [ObservableProperty]
    public partial string Need { get; set; } = string.Empty;

    /// <summary>Need colour.</summary>
    [ObservableProperty]
    public partial TextKind NeedKind { get; set; }

    /// <summary>"Indiana, USA · 20 m FT8 · 25 W".</summary>
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

    /// <summary>
    /// Shows the notice when the snapshot carries a contact not shown yet. UI thread. The contact it replaces is let go
    /// at once; while an edit is open the new one waits (held) and shows when the edit is done.
    /// </summary>
    public void Apply(LoggedContact? logged)
    {
        if (logged is null || logged.Id == _shownId) return;
        _shownId = logged.Id;
        if (IsEditing)
        {
            if (_waiting?.QsoId is { } older) _store?.Release(older);
            _waiting = logged;
            if (logged.QsoId is { } id) _store?.Keep(id, true);
            return;
        }
        if (IsOpen && _qsoId is { } previous) _store?.Release(previous);
        Show(logged);
    }

    /// <summary>Counts down; called every 200 ms by the window.</summary>
    public void Tick()
    {
        if (!IsOpen || IsEditing) return;
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
            Finish();
            return;
        }
        Remaining = left / ShowFor;
        Countdown = $"Closes in {(int)Math.Ceiling(left.TotalSeconds)} s";
    }

    /// <summary>The pointer entered or left the notice. Leaving starts the full countdown again.</summary>
    public void Hover(bool over)
    {
        if (IsEditing) return;
        _hovered = over;
        if (!over) _closesAtUtc = _utcNow() + ShowFor;
        if (IsOpen && _qsoId is { } id) _store?.Keep(id, over);
        Tick();
    }

    /// <summary>Opens the editor for the contact shown (a click on the notice).</summary>
    [RelayCommand]
    private void Edit()
    {
        if (!IsOpen || IsEditing || _store is null || _qsoId is not { } id || _store.Get(id) is not { } q) return;
        _store.Keep(id, true);
        Editing = new QsoEditViewModel(q.Clone());
        EditError = string.Empty;
        IsEditing = true;
    }

    /// <summary>Saves the edit, then lets the contact go with it.</summary>
    [RelayCommand]
    private void SaveEdit()
    {
        if (Editing is null || _store is null) return;
        if (string.IsNullOrWhiteSpace(Editing.Call))
        {
            EditError = "Enter the call.";
            return;
        }
        _store.Save(Editing.Apply());
        Finish();
    }

    /// <summary>Drops the edit; the contact goes as it was logged.</summary>
    [RelayCommand]
    private void CancelEdit() => Finish();

    /// <summary>Closes the notice now; the contact goes as it is.</summary>
    [RelayCommand]
    private void Close() => Finish();

    private void Show(LoggedContact logged)
    {
        // A snapshot that arrives late (window busy) still gets the full time from now, not from when it was logged.
        Call = logged.Call;
        Need = StationText.Need(logged.Need);
        NeedKind = StationRowViewModel.KindOf(logged.Need);
        Subline = SublineOf(logged);
        Reports = ReportsOf(logged);
        _qsoId = logged.QsoId;
        CanEdit = _store is not null && _qsoId is not null;
        _hovered = false;
        _closesAtUtc = _utcNow() + ShowFor;
        IsOpen = true;
        Tick();
    }

    // Closes the notice and lets its contact go; a contact that waited behind an edit shows next with a fresh hold.
    private void Finish()
    {
        if (_qsoId is { } id) _store?.Release(id);
        _qsoId = null;
        Editing = null;
        IsEditing = false;
        IsOpen = false;
        _hovered = false;
        if (_waiting is not { } next) return;
        _waiting = null;
        if (next.QsoId is { } nextId) _store?.Keep(nextId, false);
        Show(next);
    }

    /// <summary>"Indiana, USA · 20 m FT8 · 25 W".</summary>
    public static string SublineOf(LoggedContact c) =>
        string.Join(" · ", new[] { c.Place, $"{BandPlan.Display(c.Band)} {ModeInfo.Name(c.Mode)}", PowerOf(c) }.Where(x => x.Length > 0));

    /// <summary>"25 W", or empty when no power was logged.</summary>
    public static string PowerOf(LoggedContact c) => string.IsNullOrWhiteSpace(c.PowerWatts) ? string.Empty : $"{c.PowerWatts} W";

    /// <summary>"Sent -12 · Received -08 · 14:30 UTC", shared with the phone (which leaves the time off).</summary>
    public static string ReportsOf(LoggedContact c, bool withTime = true) =>
        $"Sent {(c.ReportSent is { } s ? LogComment.Rst(c.Mode, s) : ContactPanelViewModel.NotYet)} · Received {(c.ReportReceived is { } r ? LogComment.Rst(c.Mode, r) : ContactPanelViewModel.NotYet)}"
        + (withTime ? $" · {OperateViewModel.Hm(c.LoggedUtc)}" : string.Empty);
}
