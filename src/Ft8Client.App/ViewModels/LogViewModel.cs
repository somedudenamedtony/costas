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
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>The Log view: every contact, search and filters, edit, delete, import, export, sync.</summary>
public sealed partial class LogViewModel : ObservableObject
{
    /// <summary>The band choice that shows every band.</summary>
    public const string AllBands = "All bands";

    /// <summary>The mode choice that shows every mode.</summary>
    public const string AllModes = "All modes";

    /// <summary>The date choice that shows every date.</summary>
    public const string AllTime = "All time";

    /// <summary>Contacts since 00:00 UTC.</summary>
    public const string Today = "Today (UTC)";

    /// <summary>Contacts in the last 7 UTC days, today included.</summary>
    public const string Last7Days = "Last 7 days";

    /// <summary>Contacts in the last 30 UTC days, today included.</summary>
    public const string Last30Days = "Last 30 days";

    /// <summary>Contacts since 1 January.</summary>
    public const string ThisYear = "This year";

    /// <summary>The confirmation choice that shows every contact.</summary>
    public const string AnyConfirmation = "Confirmed or not";

    /// <summary>Confirmed contacts only.</summary>
    public const string ConfirmedOnly = "Confirmed";

    /// <summary>Unconfirmed contacts only.</summary>
    public const string NotConfirmed = "Not confirmed";

    private const int MaxRows = 5000;

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

    /// <summary>Downloads LoTW confirmations; returns a message. Null when LoTW is not available.</summary>
    public Func<CancellationToken, Task<string>>? LotwDownload { get; init; }

    /// <summary>Signs and uploads new contacts to LoTW through TQSL; returns a message.</summary>
    public Func<CancellationToken, Task<string>>? LotwUpload { get; init; }

    /// <summary>Band filter choices: "All bands" then the bands in the log, in band plan order.</summary>
    public ObservableCollection<string> BandChoices { get; } = [AllBands];

    /// <summary>Mode filter choices.</summary>
    public IReadOnlyList<string> ModeChoices { get; } = [AllModes, "FT8", "FT4"];

    /// <summary>Date filter choices.</summary>
    public IReadOnlyList<string> DateChoices { get; } = [AllTime, Today, Last7Days, Last30Days, ThisYear];

    /// <summary>Confirmation filter choices.</summary>
    public IReadOnlyList<string> ConfirmedChoices { get; } = [AnyConfirmation, ConfirmedOnly, NotConfirmed];

    /// <summary>Selected band filter.</summary>
    [ObservableProperty]
    public partial string BandFilter { get; set; } = AllBands;

    /// <summary>Selected mode filter.</summary>
    [ObservableProperty]
    public partial string ModeFilter { get; set; } = AllModes;

    /// <summary>Selected date filter.</summary>
    [ObservableProperty]
    public partial string DateFilter { get; set; } = AllTime;

    /// <summary>Selected confirmation filter.</summary>
    [ObservableProperty]
    public partial string ConfirmedFilter { get; set; } = AnyConfirmation;

    /// <summary>True when a band, mode, date or confirmation filter is set.</summary>
    [ObservableProperty]
    public partial bool Filtered { get; set; }

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

    partial void OnBandFilterChanged(string value) => Refresh();

    partial void OnModeFilterChanged(string value) => Refresh();

    partial void OnDateFilterChanged(string value) => Refresh();

    partial void OnConfirmedFilterChanged(string value) => Refresh();

    partial void OnSelectedChanged(LogRowViewModel? value) => ConfirmingDelete = false;

    /// <summary>Reloads rows.</summary>
    public void Refresh()
    {
        RefreshBandChoices();
        var filter = BuildFilter(Search, BandFilter, ModeFilter, DateFilter, ConfirmedFilter, DateTime.UtcNow);
        Filtered = filter.Narrowed;
        var rows = _qsos.List(filter, MaxRows);
        Rows.Clear();
        foreach (var q in rows) Rows.Add(new LogRowViewModel(q));
        var shown = _qsos.Totals(filter);
        var total = filter == LogFilter.All ? shown.Contacts : _qsos.Count();
        var last = _lastSync();
        Status = StatusText(shown, total, rows.Count) + " · " + (last is { } t ? $"last synced {t:HH:mm} UTC" : "not synced with QRZ");
    }

    /// <summary>Clears the band, mode, date and confirmation filters (the search text stays).</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        BandFilter = AllBands;
        ModeFilter = AllModes;
        DateFilter = AllTime;
        ConfirmedFilter = AnyConfirmation;
    }

    /// <summary>The filter for the chosen search text and filter choices.</summary>
    public static LogFilter BuildFilter(string search, string band, string mode, string date, string confirmed, DateTime nowUtc) => new(
        string.IsNullOrWhiteSpace(search) ? null : search,
        band == AllBands || string.IsNullOrEmpty(band) ? null : band,
        mode == AllModes || string.IsNullOrEmpty(mode) ? null : mode,
        FromFor(date, nowUtc),
        confirmed switch { ConfirmedOnly => true, NotConfirmed => false, _ => null });

    /// <summary>The earliest time a date choice includes, or null for all time. Days are UTC days, as the log shows them.</summary>
    public static DateTime? FromFor(string date, DateTime nowUtc) => date switch
    {
        Today => nowUtc.Date,
        Last7Days => nowUtc.Date.AddDays(-6),
        Last30Days => nowUtc.Date.AddDays(-29),
        ThisYear => new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => null,
    };

    /// <summary>
    /// "3,400 contacts · 212 countries · 1,920 confirmed", or when filtered "Showing 120 of 3,400 contacts · 41 countries · 60 confirmed".
    /// </summary>
    public static string StatusText(LogTotals shown, int total, int listed)
    {
        var inv = CultureInfo.InvariantCulture;
        var head = shown.Contacts == total
            ? $"{total.ToString("N0", inv)} {(total == 1 ? "contact" : "contacts")}"
            : $"Showing {shown.Contacts.ToString("N0", inv)} of {total.ToString("N0", inv)} contacts";
        var text = $"{head} · {shown.Countries.ToString("N0", inv)} {(shown.Countries == 1 ? "country" : "countries")} · {shown.Confirmed.ToString("N0", inv)} confirmed";
        if (listed < shown.Contacts) text += $" · newest {listed.ToString("N0", inv)} listed";
        return text;
    }

    private void RefreshBandChoices()
    {
        var bands = _qsos.Bands().OrderBy(BandOrder).ThenBy(b => b, StringComparer.Ordinal).ToList();
        if (BandChoices.Skip(1).SequenceEqual(bands)) return;
        var keep = BandFilter;
        while (BandChoices.Count > 1) BandChoices.RemoveAt(1);
        foreach (var b in bands) BandChoices.Add(b);
        // Removing the selected item clears the selection; put it back if the band is still logged.
        BandFilter = BandChoices.Contains(keep) ? keep : AllBands;
    }

    private static int BandOrder(string band)
    {
        for (var i = 0; i < BandPlan.Bands.Count; i++)
        {
            if (string.Equals(BandPlan.Bands[i].Band, band, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return int.MaxValue;
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

    /// <summary>Downloads new LoTW confirmations now.</summary>
    [RelayCommand]
    private Task LotwDownloadNowAsync() => RunAsync(LotwDownload, "Downloading LoTW confirmations…");

    /// <summary>Signs and uploads contacts LoTW does not have yet, through TQSL.</summary>
    [RelayCommand]
    private Task LotwUploadNowAsync() => RunAsync(LotwUpload, "Signing and uploading to LoTW with TQSL…");

    private async Task RunAsync(Func<CancellationToken, Task<string>>? action, string busy)
    {
        if (action is null) return;
        Syncing = true;
        Message = busy;
        try
        {
            Message = await action(CancellationToken.None);
        }
        finally
        {
            Syncing = false;
            Refresh();
        }
    }

    private bool HasSelection() => Selected is not null;
}
