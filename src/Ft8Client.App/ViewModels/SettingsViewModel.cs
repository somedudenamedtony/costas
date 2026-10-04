// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Services;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Data.Secrets;

namespace Ft8Client.App.ViewModels;

/// <summary>Settings. Changes are saved together with Save.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppHost _host;

    /// <summary>Creates the view model from current settings.</summary>
    public SettingsViewModel(AppHost host, bool rankingOnly = false)
    {
        _host = host;
        RankingOnly = rankingOnly;
        var s = host.Settings.Current;
        Callsign = s.Profile.Callsign;
        Grid = s.Profile.Grid;
        Power = s.Profile.PowerWatts.ToString(CultureInfo.InvariantCulture);
        TxGain = s.Profile.Audio.TxGainDb.ToString("0.0", CultureInfo.InvariantCulture);
        Watchdog = s.Operating.WatchdogMinutes.ToString(CultureInfo.InvariantCulture);
        RetryLimit = s.Operating.RetryLimit.ToString(CultureInfo.InvariantCulture);
        Signoff = s.Operating.Signoff;
        AutoLog = s.Operating.AutoLog;
        CqParity = s.Operating.CqParity;
        ClockBlock = s.Operating.ClockBlockSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        foreach (var t in SettingsMapper.ToRanking(s.Ranking).TierOrder) TierOrder.Add(new TierItem(t, StationText.Need(t)));
        PreferHearsMe = s.Ranking.PreferHearsMe;
        OnlyCallingCq = s.Ranking.OnlyCallingCq;
        HideWorked = s.Ranking.HideWorked;
        SkipLongShots = s.Ranking.SkipLongShots;
        ConfirmedOnly = s.Ranking.ConfirmedOnly;
        LogbookEnabled = s.Qrz.LogbookEnabled;
        LookupEnabled = s.Qrz.LookupEnabled;
        UploadOnComplete = s.Qrz.UploadOnComplete;
        SyncMinutes = s.Qrz.SyncMinutes.ToString(CultureInfo.InvariantCulture);
        LogbookKey = host.Secrets.Get(SecretNames.QrzLogbookKey(s.Profile.Id)) ?? string.Empty;
        QrzUser = host.Secrets.Get(SecretNames.QrzUsername) ?? string.Empty;
        QrzPassword = host.Secrets.Get(SecretNames.QrzPassword) ?? string.Empty;
        FeedEnabled = s.PskReporter.FeedEnabled;
        UploadSpots = s.PskReporter.UploadSpots;
        UdpEnabled = s.Udp.Enabled;
        UdpAddress = s.Udp.Address;
        UdpPort = s.Udp.Port.ToString(CultureInfo.InvariantCulture);
        SaveWav = s.Files.SaveWav;
        Retention = s.Files.RetentionDays.ToString(CultureInfo.InvariantCulture);
        Theme = s.Appearance.Theme;
        Units = s.Appearance.Units;
        Jt9Path = s.Paths.Jt9 ?? string.Empty;
        HamlibDir = s.Paths.HamlibDir ?? string.Empty;
        SecretsNote = host.Secrets.IsPersistent
            ? "Keys and passwords are kept in the system credential store, not in the settings file."
            : "No system credential store is available here: keys and passwords last until the app closes.";
    }

    /// <summary>Raised when the window should close.</summary>
    public event Action? Close;

    /// <summary>Opened from "How the line is ordered": show only ranking.</summary>
    public bool RankingOnly { get; }

    /// <summary>Show every section.</summary>
    public bool AllSections => !RankingOnly;

    /// <summary>Message line.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>Call.</summary>
    [ObservableProperty]
    public partial string Callsign { get; set; }

    /// <summary>Grid.</summary>
    [ObservableProperty]
    public partial string Grid { get; set; }

    /// <summary>Power in watts.</summary>
    [ObservableProperty]
    public partial string Power { get; set; }

    /// <summary>Tx gain dBFS.</summary>
    [ObservableProperty]
    public partial string TxGain { get; set; }

    /// <summary>Watchdog minutes.</summary>
    [ObservableProperty]
    public partial string Watchdog { get; set; }

    /// <summary>Retry limit.</summary>
    [ObservableProperty]
    public partial string RetryLimit { get; set; }

    /// <summary>Sign-offs.</summary>
    public IReadOnlyList<string> Signoffs { get; } = ["RR73", "RRR"];

    /// <summary>Sign-off.</summary>
    [ObservableProperty]
    public partial string Signoff { get; set; }

    /// <summary>Auto log.</summary>
    [ObservableProperty]
    public partial bool AutoLog { get; set; }

    /// <summary>Parities.</summary>
    public IReadOnlyList<string> Parities { get; } = ["even", "odd"];

    /// <summary>CQ parity.</summary>
    [ObservableProperty]
    public partial string CqParity { get; set; }

    /// <summary>Clock block seconds.</summary>
    [ObservableProperty]
    public partial string ClockBlock { get; set; }

    /// <summary>Tier order.</summary>
    public ObservableCollection<TierItem> TierOrder { get; } = [];

    /// <summary>Prefer stations that hear me.</summary>
    [ObservableProperty]
    public partial bool PreferHearsMe { get; set; }

    /// <summary>Only stations calling CQ.</summary>
    [ObservableProperty]
    public partial bool OnlyCallingCq { get; set; }

    /// <summary>Hide worked.</summary>
    [ObservableProperty]
    public partial bool HideWorked { get; set; }

    /// <summary>Skip long shots.</summary>
    [ObservableProperty]
    public partial bool SkipLongShots { get; set; }

    /// <summary>Confirmed only.</summary>
    [ObservableProperty]
    public partial bool ConfirmedOnly { get; set; }

    /// <summary>QRZ logbook.</summary>
    [ObservableProperty]
    public partial bool LogbookEnabled { get; set; }

    /// <summary>QRZ lookups.</summary>
    [ObservableProperty]
    public partial bool LookupEnabled { get; set; }

    /// <summary>Upload on completion.</summary>
    [ObservableProperty]
    public partial bool UploadOnComplete { get; set; }

    /// <summary>Sync minutes.</summary>
    [ObservableProperty]
    public partial string SyncMinutes { get; set; }

    /// <summary>Logbook key.</summary>
    [ObservableProperty]
    public partial string LogbookKey { get; set; }

    /// <summary>QRZ username.</summary>
    [ObservableProperty]
    public partial string QrzUser { get; set; }

    /// <summary>QRZ password.</summary>
    [ObservableProperty]
    public partial string QrzPassword { get; set; }

    /// <summary>Where secrets are kept.</summary>
    public string SecretsNote { get; }

    /// <summary>PSK feed.</summary>
    [ObservableProperty]
    public partial bool FeedEnabled { get; set; }

    /// <summary>Upload spots.</summary>
    [ObservableProperty]
    public partial bool UploadSpots { get; set; }

    /// <summary>UDP enabled.</summary>
    [ObservableProperty]
    public partial bool UdpEnabled { get; set; }

    /// <summary>UDP address.</summary>
    [ObservableProperty]
    public partial string UdpAddress { get; set; }

    /// <summary>UDP port.</summary>
    [ObservableProperty]
    public partial string UdpPort { get; set; }

    /// <summary>WAV choices.</summary>
    public IReadOnlyList<string> WavChoices { get; } = ["none", "decoded", "all"];

    /// <summary>WAV saving.</summary>
    [ObservableProperty]
    public partial string SaveWav { get; set; }

    /// <summary>Retention days.</summary>
    [ObservableProperty]
    public partial string Retention { get; set; }

    /// <summary>Themes.</summary>
    public IReadOnlyList<string> Themes { get; } = ["system", "light", "dark"];

    /// <summary>Theme.</summary>
    [ObservableProperty]
    public partial string Theme { get; set; }

    /// <summary>Unit choices.</summary>
    public IReadOnlyList<string> UnitChoices { get; } = ["miles", "km"];

    /// <summary>Units.</summary>
    [ObservableProperty]
    public partial string Units { get; set; }

    /// <summary>jt9 path.</summary>
    [ObservableProperty]
    public partial string Jt9Path { get; set; }

    /// <summary>Hamlib folder.</summary>
    [ObservableProperty]
    public partial string HamlibDir { get; set; }

    /// <summary>Moves a tier up.</summary>
    [RelayCommand]
    private void TierUp(TierItem t)
    {
        var i = TierOrder.IndexOf(t);
        if (i > 0) TierOrder.Move(i, i - 1);
    }

    /// <summary>Moves a tier down.</summary>
    [RelayCommand]
    private void TierDown(TierItem t)
    {
        var i = TierOrder.IndexOf(t);
        if (i >= 0 && i < TierOrder.Count - 1) TierOrder.Move(i, i + 1);
    }

    /// <summary>Saves and closes.</summary>
    [RelayCommand]
    private void Save()
    {
        static int Int(string s, int min, int max, int fallback) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, min, max) : fallback;
        static double Dbl(string s, double min, double max, double fallback) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, min, max) : fallback;

        if (!RankingOnly && !Core.Messages.Callsign.IsValid(Callsign.Trim().ToUpperInvariant()))
        {
            Message = "Enter a valid callsign.";
            return;
        }
        _host.Settings.Update(s =>
        {
            s.Ranking.TierOrder = SettingsMapper.TierNames(TierOrder.Select(t => t.Tag));
            s.Ranking.PreferHearsMe = PreferHearsMe;
            s.Ranking.OnlyCallingCq = OnlyCallingCq;
            s.Ranking.HideWorked = HideWorked;
            s.Ranking.SkipLongShots = SkipLongShots;
            s.Ranking.ConfirmedOnly = ConfirmedOnly;
            if (RankingOnly) return;
            s.Profile.Callsign = Callsign.Trim().ToUpperInvariant();
            s.Profile.Grid = Grid.Trim().ToUpperInvariant();
            s.Profile.PowerWatts = Int(Power, 1, 2000, s.Profile.PowerWatts);
            s.Profile.Audio.TxGainDb = Dbl(TxGain, -40, 0, -6);
            s.Operating.WatchdogMinutes = Int(Watchdog, 1, 60, 6);
            s.Operating.RetryLimit = Int(RetryLimit, 1, 20, 4);
            s.Operating.Signoff = Signoff;
            s.Operating.AutoLog = AutoLog;
            s.Operating.CqParity = CqParity;
            s.Operating.ClockBlockSeconds = Dbl(ClockBlock, 0.5, 10, 2);
            s.Qrz.LogbookEnabled = LogbookEnabled;
            s.Qrz.LookupEnabled = LookupEnabled;
            s.Qrz.UploadOnComplete = UploadOnComplete;
            s.Qrz.SyncMinutes = Int(SyncMinutes, 5, 1440, 15);
            s.PskReporter.FeedEnabled = FeedEnabled;
            s.PskReporter.UploadSpots = UploadSpots;
            s.Udp.Enabled = UdpEnabled;
            s.Udp.Address = UdpAddress.Trim();
            s.Udp.Port = Int(UdpPort, 1, 65535, 2237);
            s.Files.SaveWav = SaveWav;
            s.Files.RetentionDays = Int(Retention, 1, 3650, 30);
            s.Appearance.Theme = Theme;
            s.Appearance.Units = Units;
            s.Paths.Jt9 = string.IsNullOrWhiteSpace(Jt9Path) ? null : Jt9Path.Trim();
            s.Paths.HamlibDir = string.IsNullOrWhiteSpace(HamlibDir) ? null : HamlibDir.Trim();
        });
        if (!RankingOnly)
        {
            var id = _host.Settings.Current.Profile.Id;
            Set(SecretNames.QrzLogbookKey(id), LogbookKey.Trim());
            Set(SecretNames.QrzUsername, QrzUser.Trim());
            Set(SecretNames.QrzPassword, QrzPassword);
            _host.Transmitter.GainDb = _host.Settings.Current.Profile.Audio.TxGainDb;
            _host.RebuildLogIndex();
        }
        Close?.Invoke();
    }

    /// <summary>Closes without saving.</summary>
    [RelayCommand]
    private void Cancel() => Close?.Invoke();

    private void Set(string name, string value)
    {
        if (string.IsNullOrEmpty(value)) _host.Secrets.Delete(name);
        else _host.Secrets.Set(name, value);
    }
}
