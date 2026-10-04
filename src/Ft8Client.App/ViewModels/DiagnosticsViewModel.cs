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
using Ft8Client.Core;
using Ft8Client.App.Services;
using Ft8Client.Data.Secrets;
using Ft8Client.Rig;

namespace Ft8Client.App.ViewModels;

/// <summary>Diagnostics: the numbers behind the indicators, and the support bundle.</summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly AppHost _host;

    /// <summary>Creates the view model.</summary>
    public DiagnosticsViewModel(AppHost host)
    {
        _host = host;
        Refresh();
    }

    /// <summary>Rows.</summary>
    public ObservableCollection<DiagRow> Rows { get; } = [];

    /// <summary>Message.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>Reloads values.</summary>
    [RelayCommand]
    public void Refresh()
    {
        var s = _host.Session.Snapshot;
        Rows.Clear();
        void Add(string k, string v) => Rows.Add(new DiagRow(k, v));
        Add("Version", $"{AppInfo.ProductName} {AppInfo.Version}");
        Add("Mode", _host.Simulating ? "Simulation" : "Live");
        Add("Data folder", _host.Paths.Root);
        Add("jt9", _host.Jt9Path ?? "not found");
        Add("Decode time, last slot", $"{s.LastDecodeTime.TotalSeconds:0.00} s");
        Add("Median DT, last slot", s.MedianDt is { } dt ? $"{dt:+0.00;-0.00} s" : "—");
        Add("Clock offset (SNTP)", _host.Sntp is { } n ? $"{n.Offset.TotalSeconds:+0.000;-0.000} s (round trip {n.RoundTrip.TotalMilliseconds:0} ms)" : "not measured");
        Add("Audio dropouts", _host.Pump?.Dropouts.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—");
        Add("Input level", _host.Pump is { } p ? $"{p.LevelDbfs:0} dBFS" : "—");
        if (_host.Rig is RigctldRig r)
        {
            Add("rigctld", r.ProcessRunning ? "running" : "stopped");
            Add("rigctld restarts", r.Restarts.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Add("CAT round trip", $"{r.LastRoundTrip.TotalMilliseconds:0} ms");
        }
        else
        {
            Add("Radio", _host.Rig.Name);
        }
        foreach (var (name, st) in s.Services) Add($"Status: {name}", $"{st.Health} · {st.Message}");
        Add("Upload queue", _host.Uploads.Count().ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("Contacts in log", _host.Qsos.Count().ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("Country file", _host.Countries.Version);
        Add("Credential store", _host.Secrets.IsPersistent ? "system" : "memory only");
    }

    /// <summary>Writes a zip of logs and settings (no secrets) next to the data folder.</summary>
    [RelayCommand]
    private void SaveBundle()
    {
        var path = Path.Combine(_host.Paths.Root, $"diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        var secrets = new[] { SecretNames.QrzLogbookKey(_host.Settings.Current.Profile.Id), SecretNames.QrzUsername, SecretNames.QrzPassword }
            .Select(_host.Secrets.Get).Where(v => v is not null).Select(v => v!).ToList();
        DiagnosticsBundle.Write(path, _host.Paths.Settings, _host.Paths.Logs, Rows.Select(r => (r.Key, r.Value)), secrets);
        Message = $"Saved {path}";
    }
}
