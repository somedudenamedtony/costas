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
using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Data.Secrets;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.ViewModels;

/// <summary>Settings. Changes are saved together with Save.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly string _loadedTxGain;
    private readonly string _loadedProfile;
    private ProfileDraft? _shownProfile;

    /// <summary>Creates the view model from current settings.</summary>
    public SettingsViewModel(AppHost host, bool rankingOnly = false)
    {
        _host = host;
        SelectedPage = rankingOnly ? WhoToCallPage : 0;
        var s = host.Settings.Current;
        foreach (var p in s.Profiles)
        {
            Profiles.Add(new ProfileDraft
            {
                Id = p.Id, Name = p.Name, Callsign = p.Callsign, Grid = p.Grid, Power = p.PowerWatts.ToString(CultureInfo.InvariantCulture),
                LogbookKey = host.Secrets.Get(SecretNames.QrzLogbookKey(p.Id)) ?? string.Empty,
            });
        }
        _loadedProfile = s.Profile.Id;
        var active = Profiles.First(d => d.Id == s.Profile.Id);
        (Callsign, Grid, Power, LogbookKey) = (active.Callsign, active.Grid, active.Power, active.LogbookKey);
        _shownProfile = active;
        SelectedProfile = active;
        TxGain = _loadedTxGain = TxLevelController.LevelFor(s.Profile.Audio, s.Operating.Band).ToString("0.0", CultureInfo.InvariantCulture);
        TxGainLabel = $"Audio level on {s.Operating.Band} (dB)";
        AutoTxLevel = s.Profile.Audio.AutoTxLevel;
        Watchdog = s.Operating.WatchdogMinutes.ToString(CultureInfo.InvariantCulture);
        RetryLimit = s.Operating.RetryLimit.ToString(CultureInfo.InvariantCulture);
        Signoff = s.Operating.Signoff;
        CqModifier = s.Operating.CqModifier;
        GridInReply = s.Operating.GridInReply;
        AnswerSignoff = s.Operating.AnswerSignoff == "RR73" ? "RR73" : "73";
        FreeTextAfter = s.Operating.FreeTextAfter;
        AutoLog = s.Operating.AutoLog;
        ChimeOnCaller = s.Operating.ChimeOnCaller;
        CqParity = s.Operating.CqParity;
        ClockBlock = s.Operating.ClockBlockSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        foreach (var t in SettingsMapper.ToRanking(s.Ranking).TierOrder) TierOrder.Add(new TierItem(t, StationText.Need(t)));
        PreferHearsMe = s.Ranking.PreferHearsMe;
        OnlyCallingCq = s.Ranking.OnlyCallingCq;
        HideWorked = s.Ranking.HideWorked;
        SkipLongShots = s.Ranking.SkipLongShots;
        ConfirmedOnly = s.Ranking.ConfirmedOnly;
        KeepCqCallers = KeepChoiceFor(s.Ranking.KeepCqCallersMinutes);
        LogbookEnabled = s.Qrz.LogbookEnabled;
        UploadOnComplete = s.Qrz.UploadOnComplete;
        SyncMinutes = s.Qrz.SyncMinutes.ToString(CultureInfo.InvariantCulture);
        QrzUser = host.Secrets.Get(SecretNames.QrzUsername) ?? string.Empty;
        QrzPassword = host.Secrets.Get(SecretNames.QrzPassword) ?? string.Empty;
        FeedEnabled = s.PskReporter.FeedEnabled;
        PotaSpots = s.Pota.SpotsEnabled;
        CompareNeighbours = s.PskReporter.CompareNeighbours;
        BandActivity = s.PskReporter.BandActivity;
        UploadSpots = s.PskReporter.UploadSpots;
        UdpEnabled = s.Udp.Enabled;
        UdpAddress = s.Udp.Address;
        UdpPort = s.Udp.Port.ToString(CultureInfo.InvariantCulture);
        RemoteEnabled = s.Remote.Enabled;
        RemotePort = s.Remote.Port.ToString(CultureInfo.InvariantCulture);
        RefreshRemote();
        host.Remote.Changed += OnRemoteChanged;
        _remoteLoaded = true;
        if (host.Remote.Running && host.Remote.Control.Tokens.Count == 0) PairPhone();
        SaveWav = s.Files.SaveWav;
        Retention = s.Files.RetentionDays.ToString(CultureInfo.InvariantCulture);
        Theme = s.Appearance.Theme;
        Units = s.Appearance.Units;
        Jt9Path = s.Paths.Jt9 ?? string.Empty;
        HamlibDir = s.Paths.HamlibDir ?? string.Empty;
        CheckUpdates = s.Updates.CheckEnabled;
        ShowDeveloper = s.Developer.Enabled;
        DeveloperMode = s.Developer.Enabled;
        ReplaySamples = s.Developer.ReplaySamples;
        SimulatedPartner = s.Developer.SimulatedPartner;
        SecretsNote = host.Secrets.IsPersistent
            ? "The API key and password are kept in the system credential store, not in the settings file."
            : "No system credential store is available here: the API key and password are kept only until the app closes.";
    }

    /// <summary>Raised when the window should close.</summary>
    public event Action? Close;

    /// <summary>Index of the Who to call page.</summary>
    public const int WhoToCallPage = 2;

    /// <summary>The page shown when the window opens (Who to call when opened from "Change").</summary>
    [ObservableProperty]
    public partial int SelectedPage { get; set; }

    /// <summary>Result of "Test key".</summary>
    [ObservableProperty]
    public partial string KeyStatus { get; set; } = string.Empty;

    /// <summary>Tests the logbook API key with QRZ.</summary>
    [RelayCommand]
    private async Task TestKey()
    {
        if (string.IsNullOrWhiteSpace(LogbookKey))
        {
            KeyStatus = "Paste the key first.";
            return;
        }
        KeyStatus = "Testing…";
        var (_, msg) = await _host.Qrz.TestKeyAsync(LogbookKey.Trim(), CancellationToken.None);
        KeyStatus = msg;
    }

    /// <summary>Result of "Test login".</summary>
    [ObservableProperty]
    public partial string LookupStatus { get; set; } = string.Empty;

    /// <summary>Tests the QRZ username and password used for callsign lookups.</summary>
    [RelayCommand]
    private async Task TestLookup()
    {
        if (string.IsNullOrWhiteSpace(QrzUser) || string.IsNullOrEmpty(QrzPassword))
        {
            LookupStatus = "Enter your QRZ username and password first.";
            return;
        }
        LookupStatus = "Testing…";
        var (_, msg) = await _host.Qrz.TestLookupAsync(QrzUser.Trim(), QrzPassword, CancellationToken.None);
        LookupStatus = msg;
    }

    /// <summary>Opens the QRZ Logbook, where the API key is shown.</summary>
    [RelayCommand]
    private static void OpenQrz() => Services.Browser.Open(Services.Browser.QrzLogbook);

    /// <summary>Message line.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>Station profiles as edited here.</summary>
    public ObservableCollection<ProfileDraft> Profiles { get; } = [];

    /// <summary>The profile the Station page shows, which becomes the active one on Save.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    public partial ProfileDraft? SelectedProfile { get; set; }

    partial void OnSelectedProfileChanged(ProfileDraft? value)
    {
        // The list can briefly clear its selection while items change; keep showing the last profile.
        if (value is null || ReferenceEquals(value, _shownProfile)) return;
        StoreFields();
        (Callsign, Grid, Power, LogbookKey) = (value.Callsign, value.Grid, value.Power, value.LogbookKey);
        KeyStatus = string.Empty;
        _shownProfile = value;
    }

    /// <summary>Adds a profile, a copy of the one shown (same radio and audio), and shows it.</summary>
    [RelayCommand]
    private void NewProfile()
    {
        StoreFields();
        var from = SelectedProfile ?? Profiles[0];
        var draft = new ProfileDraft
        {
            Id = "p" + Guid.NewGuid().ToString("N")[..8], CopyOf = from.Id, Name = NewProfileName(Profiles.Select(p => p.Name)),
            Callsign = from.Callsign, Grid = from.Grid, Power = from.Power,
        };
        Profiles.Add(draft);
        SelectedProfile = draft;
        DeleteProfileCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Removes the profile shown. The last profile cannot be removed.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private void DeleteProfile()
    {
        if (SelectedProfile is not { } gone || Profiles.Count < 2) return;
        _shownProfile = null;
        Profiles.Remove(gone);
        SelectedProfile = Profiles[0];
        DeleteProfileCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteProfile() => Profiles.Count > 1;

    /// <summary>"Portable", then "Portable 2", "Portable 3"… avoiding names in use.</summary>
    public static string NewProfileName(IEnumerable<string> taken)
    {
        var names = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        if (!names.Contains("Portable")) return "Portable";
        for (var i = 2; ; i++)
        {
            var n = $"Portable {i}";
            if (!names.Contains(n)) return n;
        }
    }

    private void StoreFields()
    {
        if (_shownProfile is null) return;
        _shownProfile.Callsign = Callsign;
        _shownProfile.Grid = Grid;
        _shownProfile.Power = Power;
        _shownProfile.LogbookKey = LogbookKey;
    }

    /// <summary>
    /// Writes the edited profiles into settings: removed ones are dropped, new ones copied from their source, names, calls,
    /// grids and power set, and <paramref name="active"/> made the active profile.
    /// </summary>
    public static void ApplyProfiles(AppSettings s, IReadOnlyList<ProfileDraft> drafts, string active)
    {
        static int Watts(string text, int fallback) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, 1, 2000) : fallback;

        var old = s.Profiles.ToList();
        var result = new List<StationProfile>();
        foreach (var d in drafts)
        {
            var p = old.FirstOrDefault(x => x.Id == d.Id);
            if (p is null)
            {
                var source = old.FirstOrDefault(x => x.Id == d.CopyOf) ?? old.FirstOrDefault() ?? new StationProfile();
                p = source.CopyAs(d.Id, d.Name);
            }
            p.Name = string.IsNullOrWhiteSpace(d.Name) ? p.Name : d.Name.Trim();
            p.Callsign = d.Callsign.Trim().ToUpperInvariant();
            p.Grid = d.Grid.Trim().ToUpperInvariant();
            p.PowerWatts = Watts(d.Power, p.PowerWatts);
            result.Add(p);
        }
        s.Profiles = result;
        s.ActiveProfile = active;
    }

    /// <summary>Call.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial string Callsign { get; set; }

    /// <summary>Grid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial string Grid { get; set; }

    /// <summary>Power in watts.</summary>
    [ObservableProperty]
    public partial string Power { get; set; }

    /// <summary>Tx gain dBFS on the current band.</summary>
    [ObservableProperty]
    public partial string TxGain { get; set; }

    /// <summary>"Audio level on 20m (dB)".</summary>
    public string TxGainLabel { get; }

    /// <summary>Adjust the transmit level from the radio's ALC.</summary>
    [ObservableProperty]
    public partial bool AutoTxLevel { get; set; }

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
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial string Signoff { get; set; }

    /// <summary>CQ modifier (DX, POTA, ...); empty for a plain CQ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial string CqModifier { get; set; }

    /// <summary>Send my grid when answering a CQ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial bool GridInReply { get; set; }

    /// <summary>Choices for my last message when I answered a CQ.</summary>
    public IReadOnlyList<string> AnswerSignoffs { get; } = ["73", "RR73"];

    /// <summary>My last message when I answered a CQ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial string AnswerSignoff { get; set; }

    /// <summary>Free text sent after my last message; empty for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessagePreview), nameof(MessageProblem))]
    public partial string FreeTextAfter { get; set; }

    /// <summary>The messages as they will be sent with the settings on screen, one per line.</summary>
    public string MessagePreview
    {
        get
        {
            var s = MessageSettings();
            var me = Callsign.Trim().ToUpperInvariant();
            if (me.Length == 0) me = "MYCALL";
            var grid = Core.Messages.Grid.Grid4(Grid.Trim().ToUpperInvariant()) ?? string.Empty;
            const string dx = "THEIRCALL";
            var lines = new List<string>
            {
                $"Calling CQ:        {OutgoingMessages.Cq(me, grid, s)}",
                $"Report:            {dx} {me} -10",
                $"Sign-off:          {dx} {me} {s.Signoff}",
                string.Empty,
                $"Answering a CQ:    {OutgoingMessages.Reply(dx, me, grid, s)}",
                $"Roger and report:  {dx} {me} R-10",
                $"Last message:      {OutgoingMessages.Final(dx, me, s)}",
            };
            if (OutgoingMessages.FreeTextAfter(s) is { } free) lines.Add($"Then:              {free}");
            return string.Join('\n', lines);
        }
    }

    /// <summary>Why the message settings cannot be used, or empty.</summary>
    public string MessageProblem => OutgoingMessages.Problem(MessageSettings(), Callsign, Grid) ?? string.Empty;

    private ContactSettings MessageSettings() => new()
    {
        Signoff = Signoff == "RRR" ? "RRR" : "RR73",
        CqModifier = CqModifier.Trim().ToUpperInvariant(),
        GridInReply = GridInReply,
        AnswerSignoff = AnswerSignoff == "RR73" ? "RR73" : "73",
        FreeTextAfter = FreeText(),
    };

    private string FreeText() => string.Join(' ', FreeTextAfter.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Auto log.</summary>
    [ObservableProperty]
    public partial bool AutoLog { get; set; }

    /// <summary>Chime when a new station calls me.</summary>
    [ObservableProperty]
    public partial bool ChimeOnCaller { get; set; }

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

    /// <summary>Choices for how long a CQ caller stays in Next in line after it was last heard, with their minutes.</summary>
    public static readonly IReadOnlyList<(string Text, int Minutes)> KeepCqChoices =
    [
        ("Only while heard every slot", 0), ("1 minute", 1), ("2 minutes", 2), ("3 minutes", 3), ("5 minutes", 5),
        ("10 minutes", 10), ("15 minutes", 15),
    ];

    /// <summary>The keep-listed choices as shown.</summary>
    public IReadOnlyList<string> KeepCqCallersChoices { get; } = KeepCqChoices.Select(c => c.Text).ToList();

    /// <summary>How long a CQ caller stays in Next in line after it was last heard.</summary>
    [ObservableProperty]
    public partial string KeepCqCallers { get; set; }

    /// <summary>The choice for a stored number of minutes (the nearest one not shorter, or the longest).</summary>
    public static string KeepChoiceFor(int minutes)
    {
        foreach (var (text, m) in KeepCqChoices) if (m >= minutes) return text;
        return KeepCqChoices[^1].Text;
    }

    /// <summary>Minutes for a shown choice; 2 when it is not one of the choices.</summary>
    public static int KeepMinutesFor(string? choice)
    {
        foreach (var (text, m) in KeepCqChoices) if (text == choice) return m;
        return 2;
    }

    /// <summary>QRZ logbook.</summary>
    [ObservableProperty]
    public partial bool LogbookEnabled { get; set; }


    /// <summary>Upload on completion.</summary>
    [ObservableProperty]
    public partial bool UploadOnComplete { get; set; }

    /// <summary>Sync minutes.</summary>
    [ObservableProperty]
    public partial string SyncMinutes { get; set; }

    /// <summary>Logbook key.</summary>
    [ObservableProperty]
    public partial string LogbookKey { get; set; }

    /// <summary>QRZ username, for callsign lookups.</summary>
    [ObservableProperty]
    public partial string QrzUser { get; set; }

    /// <summary>QRZ password, for callsign lookups.</summary>
    [ObservableProperty]
    public partial string QrzPassword { get; set; }

    /// <summary>Where secrets are kept.</summary>
    public string SecretsNote { get; }

    /// <summary>PSK feed.</summary>
    [ObservableProperty]
    public partial bool FeedEnabled { get; set; }

    /// <summary>Read the POTA spot feed to show parks under Why.</summary>
    [ObservableProperty]
    public partial bool PotaSpots { get; set; }

    /// <summary>Compare my signal with stations near me.</summary>
    [ObservableProperty]
    public partial bool CompareNeighbours { get; set; }

    /// <summary>Show in the band menu where stations near me are heard on every band.</summary>
    [ObservableProperty]
    public partial bool BandActivity { get; set; }

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

    /// <summary>Serve the remote page.</summary>
    [ObservableProperty]
    public partial bool RemoteEnabled { get; set; }

    /// <summary>Remote page port.</summary>
    [ObservableProperty]
    public partial string RemotePort { get; set; }

    /// <summary>Where to point the phone, or why the page is not served.</summary>
    [ObservableProperty]
    public partial string RemoteStatus { get; set; } = string.Empty;

    /// <summary>The pairing code, "314 159", or blank when none is shown.</summary>
    [ObservableProperty]
    public partial string PairingCode { get; set; } = string.Empty;

    /// <summary>The address to open on the phone, shown large beside the code.</summary>
    [ObservableProperty]
    public partial string PairingUrl { get; set; } = string.Empty;

    /// <summary>The other addresses of this computer, in case the first is a virtual adapter; blank when there are none.</summary>
    [ObservableProperty]
    public partial string PairingOtherUrls { get; set; } = string.Empty;

    /// <summary>"Works once, until 15:02."</summary>
    [ObservableProperty]
    public partial string PairingExpiry { get; set; } = string.Empty;

    /// <summary>True while a pairing code is shown.</summary>
    [ObservableProperty]
    public partial bool HasPairingCode { get; set; }

    /// <summary>A line after forgetting the paired phones, or blank.</summary>
    [ObservableProperty]
    public partial string PairingNote { get; set; } = string.Empty;

    /// <summary>True while the page is served (pairing works).</summary>
    [ObservableProperty]
    public partial bool RemoteRunning { get; set; }

    /// <summary>"Forget 2 paired phones".</summary>
    [ObservableProperty]
    public partial string ForgetPhonesText { get; set; } = string.Empty;

    /// <summary>True when any phone is paired.</summary>
    [ObservableProperty]
    public partial bool HasPairedPhones { get; set; }

    private bool _remoteLoaded;
    private bool _pairWhenRunning;

    // Turning the mobile connection on or off takes effect at once, so the address and a pairing code appear without
    // saving and reopening Settings.
    partial void OnRemoteEnabledChanged(bool value)
    {
        if (!_remoteLoaded) return;
        _pairWhenRunning = value;
        if (!value) ClosePairing();
        var port = int.TryParse(RemotePort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? Math.Clamp(p, 1024, 65535) : 8073;
        _host.Settings.Update(s =>
        {
            s.Remote.Enabled = value;
            s.Remote.Port = port;
        });
        RefreshRemote();
    }

    private void OnRemoteChanged() => MainViewModel.Dispatch(() =>
    {
        RefreshRemote();
        if (_pairWhenRunning && _host.Remote.Running)
        {
            _pairWhenRunning = false;
            PairPhone();
        }
    });

    /// <summary>Shows a new pairing code for a phone.</summary>
    [RelayCommand]
    private void PairPhone()
    {
        var code = _host.Remote.Control.Pairing.Open(_host.Clock.UtcNow);
        PairingCode = $"{code[..3]} {code[3..]}";
        PairingExpiry = $"Works once, until {_host.Remote.Control.Pairing.ExpiresUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture)}.";
        PairingNote = string.Empty;
        HasPairingCode = true;
        RefreshRemote();
    }

    /// <summary>Forgets every paired phone: each must be paired again.</summary>
    [RelayCommand]
    private void ForgetPhones()
    {
        _host.Remote.Control.ForgetPhones();
        PairingNote = "Paired phones forgotten. Each phone must be paired again.";
        RefreshRemote();
    }

    /// <summary>
    /// Called when the window closes: a pairing code only works while it is on screen, and this view model stops
    /// following the mobile connection.
    /// </summary>
    public void Detach()
    {
        _host.Remote.Changed -= OnRemoteChanged;
        ClosePairing();
    }

    private void ClosePairing()
    {
        _host.Remote.Control.Pairing.Close();
        HasPairingCode = false;
        PairingCode = string.Empty;
    }

    private void RefreshRemote()
    {
        var r = _host.Remote;
        RemoteRunning = r.Running;
        var urls = r.Urls();
        PairingUrl = urls.Count > 0 ? urls[0] : $"this computer's address, port {_host.Settings.Current.Remote.Port}";
        PairingOtherUrls = urls.Count > 1 ? "If that does not open, try " + string.Join(" or ", urls.Skip(1)) : string.Empty;
        RemoteStatus = r.Problem is { } p ? p
            : !r.Running ? (RemoteEnabled ? "Starting…" : "Off.")
            : urls.Count == 0 ? "On, but this computer has no address on a home network."
            : $"On. Phones open {string.Join(" or ", urls)}" + (r.Connected > 0 ? $" · {r.Connected} connected now" : string.Empty);
        if (!r.Running && HasPairingCode) ClosePairing();
        var n = r.Control.Tokens.Count;
        HasPairedPhones = n > 0;
        ForgetPhonesText = n == 1 ? "Forget the paired phone" : $"Forget {n} paired phones";
    }

    /// <summary>Check for newer builds.</summary>
    [ObservableProperty]
    public partial bool CheckUpdates { get; set; }

    /// <summary>Result of "Check now".</summary>
    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = string.Empty;

    /// <summary>Checks for a newer build now (also offers a build that was skipped before).</summary>
    [RelayCommand]
    private async Task CheckNow()
    {
        UpdateStatus = "Checking…";
        if (MessageProblem is { Length: > 0 } problem)
        {
            Message = problem;
            SelectedPage = 1;
            return;
        }
        _host.Settings.Update(s =>
        {
            s.Updates.CheckEnabled = true;
            s.Updates.SkippedTag = null;
        });
        CheckUpdates = true;
        var r = await _host.Updates.CheckAsync(CancellationToken.None);
        UpdateStatus = r is not null ? $"Costas {r.Version} is available: see the bar at the top of the main window."
            : _host.Updates.LastError is { } e ? $"Could not check: {e}"
            : $"You have the latest build ({Ft8Client.Core.AppInfo.Version}).";
    }

    /// <summary>Developer section shown (developer mode was on when the window opened).</summary>
    public bool ShowDeveloper { get; }

    /// <summary>Developer mode.</summary>
    [ObservableProperty]
    public partial bool DeveloperMode { get; set; }

    /// <summary>Replay the sample recordings instead of the sound card.</summary>
    [ObservableProperty]
    public partial bool ReplaySamples { get; set; }

    /// <summary>A simulated station answers my calls while replaying.</summary>
    [ObservableProperty]
    public partial bool SimulatedPartner { get; set; }

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

        StoreFields();
        if (Profiles.FirstOrDefault(d => !Core.Messages.Callsign.IsValid(d.Callsign.Trim().ToUpperInvariant())) is { } bad)
        {
            Message = Profiles.Count > 1 ? $"Enter a valid callsign for the {bad.Name} profile (Station page)." : "Enter a valid callsign (Station page).";
            SelectedProfile = bad;
            SelectedPage = 0;
            return;
        }
        var activeId = (SelectedProfile ?? Profiles[0]).Id;
        var removed = _host.Settings.Current.Profiles.Select(p => p.Id).Where(id => Profiles.All(d => d.Id != id)).ToList();
        _host.Settings.Update(s =>
        {
            ApplyProfiles(s, Profiles, activeId);
            s.Ranking.TierOrder = SettingsMapper.TierNames(TierOrder.Select(t => t.Tag));
            s.Ranking.PreferHearsMe = PreferHearsMe;
            s.Ranking.OnlyCallingCq = OnlyCallingCq;
            s.Ranking.HideWorked = HideWorked;
            s.Ranking.SkipLongShots = SkipLongShots;
            s.Ranking.ConfirmedOnly = ConfirmedOnly;
            s.Ranking.KeepCqCallersMinutes = KeepMinutesFor(KeepCqCallers);
            // Only a level typed here replaces the band's level, so one automatic level set meanwhile is kept.
            if (TxGain.Trim() != _loadedTxGain)
            {
                var gain = Dbl(TxGain, -40, 0, s.Profile.Audio.TxGainDb);
                s.Profile.Audio.TxGainDb = gain;
                s.Profile.Audio.TxGainByBand[s.Operating.Band] = gain;
            }
            s.Profile.Audio.AutoTxLevel = AutoTxLevel;
            s.Operating.WatchdogMinutes = Int(Watchdog, 1, 60, 6);
            s.Operating.RetryLimit = Int(RetryLimit, 1, 20, 4);
            s.Operating.Signoff = Signoff;
            s.Operating.CqModifier = CqModifier.Trim().ToUpperInvariant();
            s.Operating.GridInReply = GridInReply;
            s.Operating.AnswerSignoff = AnswerSignoff == "RR73" ? "RR73" : "73";
            s.Operating.FreeTextAfter = FreeText();
            s.Operating.AutoLog = AutoLog;
            s.Operating.ChimeOnCaller = ChimeOnCaller;
            s.Operating.CqParity = CqParity;
            s.Operating.ClockBlockSeconds = Dbl(ClockBlock, 0.5, 10, 2);
            s.Qrz.LogbookEnabled = LogbookEnabled;
            s.Qrz.LookupEnabled = !string.IsNullOrWhiteSpace(QrzUser) && !string.IsNullOrEmpty(QrzPassword);
            s.Qrz.UploadOnComplete = UploadOnComplete;
            s.Qrz.SyncMinutes = Int(SyncMinutes, 5, 1440, 15);
            s.PskReporter.FeedEnabled = FeedEnabled;
            s.Pota.SpotsEnabled = PotaSpots;
            s.PskReporter.CompareNeighbours = CompareNeighbours;
            s.PskReporter.BandActivity = BandActivity;
            s.PskReporter.UploadSpots = UploadSpots;
            s.Udp.Enabled = UdpEnabled;
            s.Udp.Address = UdpAddress.Trim();
            s.Udp.Port = Int(UdpPort, 1, 65535, 2237);
            s.Remote.Enabled = RemoteEnabled;
            s.Remote.Port = Int(RemotePort, 1024, 65535, 8073);
            s.Files.SaveWav = SaveWav;
            s.Files.RetentionDays = Int(Retention, 1, 3650, 30);
            s.Appearance.Theme = Theme;
            s.Appearance.Units = Units;
            s.Paths.Jt9 = string.IsNullOrWhiteSpace(Jt9Path) ? null : Jt9Path.Trim();
            s.Paths.HamlibDir = string.IsNullOrWhiteSpace(HamlibDir) ? null : HamlibDir.Trim();
            s.Updates.CheckEnabled = CheckUpdates;
            if (ShowDeveloper)
            {
                s.Developer.Enabled = DeveloperMode;
                s.Developer.ReplaySamples = ReplaySamples;
                s.Developer.SimulatedPartner = SimulatedPartner;
            }
        });
        foreach (var d in Profiles) Set(SecretNames.QrzLogbookKey(d.Id), d.LogbookKey.Trim());
        foreach (var id in removed) _host.Secrets.Delete(SecretNames.QrzLogbookKey(id));
        Set(SecretNames.QrzUsername, QrzUser.Trim());
        Set(SecretNames.QrzPassword, QrzPassword);
        _host.TxLevel.Apply();
        _host.RebuildLogIndex();
        // Another profile may use another radio or audio device.
        if (activeId != _loadedProfile) _ = _host.ReopenDevicesAsync();
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
