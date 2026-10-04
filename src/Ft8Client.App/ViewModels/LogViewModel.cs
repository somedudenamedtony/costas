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
using CommunityToolkit.Mvvm.Input;
using Ft8Client.Core.Geo;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>The Log view: every contact, search, edit, delete, import, export, sync.</summary>
public sealed partial class LogViewModel : ObservableObject
{
    private readonly QsoRepository _qsos;
    private readonly CountryFile _countries;
    private readonly Func<(string ProfileId, string Callsign)> _profile;
    private readonly Func<CancellationToken, Task<string>> _sync;
    private readonly Func<DateTime?> _lastSync;
    private readonly Action _changed;

    /// <summary>Creates the view model.</summary>
    public LogViewModel(QsoRepository qsos, CountryFile countries, Func<(string, string)> profile, Func<CancellationToken, Task<string>> sync,
                        Func<DateTime?> lastSync, Action changed)
    {
        _qsos = qsos;
        _countries = countries;
        _profile = profile;
        _sync = sync;
        _lastSync = lastSync;
        _changed = changed;
    }

    /// <summary>Rows, newest first.</summary>
    public ObservableCollection<LogRowViewModel> Rows { get; } = [];

    /// <summary>Search text.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    /// <summary>Selected row.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    public partial LogRowViewModel? Selected { get; set; }

    /// <summary>Status line.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Result of the last action.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>True after Delete was pressed once; the second press deletes.</summary>
    [ObservableProperty]
    public partial bool ConfirmingDelete { get; set; }

    /// <summary>The contact being edited.</summary>
    [ObservableProperty]
    public partial QsoEditViewModel? Editing { get; set; }

    /// <summary>True while syncing.</summary>
    [ObservableProperty]
    public partial bool Syncing { get; set; }

    partial void OnSearchChanged(string value) => Refresh();

    partial void OnSelectedChanged(LogRowViewModel? value) => ConfirmingDelete = false;

    /// <summary>Reloads rows.</summary>
    public void Refresh()
    {
        var rows = _qsos.List(string.IsNullOrWhiteSpace(Search) ? null : Search, 5000);
        Rows.Clear();
        foreach (var q in rows) Rows.Add(new LogRowViewModel(q));
        var total = _qsos.Count();
        var last = _lastSync();
        Status = $"{total.ToString("N0", CultureInfo.InvariantCulture)} contacts · " +
                 (last is { } t ? $"last synced {t:HH:mm} UTC" : "not synced with QRZ");
    }

    /// <summary>Deletes the selected contact locally; asks once first. QRZ deletion is a separate action.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (Selected is null) return;
        if (!ConfirmingDelete)
        {
            ConfirmingDelete = true;
            Message = $"Press Delete again to remove {Selected.Call} on {Selected.Date} from the local log. It stays in QRZ.";
            return;
        }
        _qsos.Delete(Selected.Record.Id);
        Message = $"Deleted {Selected.Call}.";
        ConfirmingDelete = false;
        Refresh();
        _changed();
    }

    /// <summary>Opens the editor.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (Selected is not null) Editing = new QsoEditViewModel(Selected.Record.Clone());
    }

    /// <summary>Saves the edit.</summary>
    [RelayCommand]
    private void SaveEdit()
    {
        if (Editing is null) return;
        var q = Editing.Apply();
        q.EntityKey = _countries.Lookup(q.Call)?.Entity.Key;
        _qsos.Update(q);
        Editing = null;
        Message = $"Saved {q.Call}.";
        Refresh();
        _changed();
    }

    /// <summary>Cancels the edit.</summary>
    [RelayCommand]
    private void CancelEdit() => Editing = null;

    /// <summary>Imports an ADIF file.</summary>
    public void Import(string path)
    {
        var (profileId, call) = _profile();
        var r = AdifImporter.Import(_qsos, File.ReadAllText(path), profileId, call, _countries);
        Message = $"Imported {r.Imported}, merged {r.Merged} duplicates, skipped {r.Skipped}.";
        Refresh();
        _changed();
    }

    /// <summary>Exports the log to an ADIF file.</summary>
    public void Export(string path)
    {
        var rows = _qsos.List();
        File.WriteAllText(path, AdifImporter.Export(rows, DateTime.UtcNow));
        Message = $"Exported {rows.Count} contacts.";
    }

    /// <summary>Syncs with QRZ now.</summary>
    [RelayCommand]
    private async Task SyncAsync()
    {
        Syncing = true;
        try
        {
            Message = await _sync(CancellationToken.None);
        }
        finally
        {
            Syncing = false;
            Refresh();
        }
    }

    private bool HasSelection() => Selected is not null;
}
