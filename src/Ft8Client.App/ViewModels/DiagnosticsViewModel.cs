// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.IO.Compression;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.Core;
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
        var secrets = SecretValues();
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            AddScrubbed(zip, _host.Paths.Settings, "settings.json", secrets);
            if (Directory.Exists(_host.Paths.Logs))
            {
                foreach (var f in Directory.EnumerateFiles(_host.Paths.Logs).OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
                    AddScrubbed(zip, f, "logs/" + Path.GetFileName(f), secrets);
            }
            var e = zip.CreateEntry("diagnostics.txt");
            using var w = new StreamWriter(e.Open());
            foreach (var (k, v) in Rows.Select(r => (r.Key, r.Value))) w.WriteLine($"{k}: {Scrub(v, secrets)}");
        }
        Message = $"Saved {path}";
    }

    /// <summary>Removes any secret value from text.</summary>
    public static string Scrub(string text, IEnumerable<string> secrets)
    {
        foreach (var s in secrets.Where(s => s.Length >= 4)) text = text.Replace(s, "[removed]", StringComparison.Ordinal);
        return text;
    }

    private List<string> SecretValues() =>
        new[] { SecretNames.QrzLogbookKey(_host.Settings.Current.Profile.Id), SecretNames.QrzUsername, SecretNames.QrzPassword }
            .Select(_host.Secrets.Get).Where(v => v is not null).Select(v => v!).ToList();

    private static void AddScrubbed(ZipArchive zip, string file, string name, List<string> secrets)
    {
        if (!File.Exists(file)) return;
        string text;
        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var r = new StreamReader(fs)) text = r.ReadToEnd();
        var e = zip.CreateEntry(name);
        using var w = new StreamWriter(e.Open());
        w.Write(Scrub(text, secrets));
    }
}

/// <summary>A diagnostics row.</summary>
/// <param name="Key">Name.</param>
/// <param name="Value">Value.</param>
public sealed record DiagRow(string Key, string Value);
