// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net.Sockets;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.PskReporter;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// PSK Reporter in the background: the live MQTT feed of who hears me, the query service as a fallback while the feed is
/// down, and (when the operator turned it on) spot uploads of what I decode. None of it is needed to operate.
/// </summary>
public sealed class PskReporterService : IAsyncDisposable
{
    /// <summary>The PSK Reporter UDP collector.</summary>
    public const string UploadHost = "report.pskreporter.info";

    /// <summary>The collector's UDP port.</summary>
    public const int UploadPort = 4739;

    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly SpotRepository _spots;
    private readonly CountryFile _countries;
    private readonly IClock _clock;
    private readonly FrequencyTable _frequencies;
    private readonly bool _uploadsAllowed;
    private readonly CancellationTokenSource _cts = new();
    private readonly PskSpotUploader _uploader;
    private volatile PskFeedClient? _feed;
    private UdpClient? _udp;
    private readonly Func<PskFeedClient> _newFeed;
    private readonly SemaphoreSlim _identityGate = new(1, 1);
    private Task _identity = Task.CompletedTask;
    private CancellationTokenSource? _queryCts;
    private volatile string _myCall = string.Empty;
    private volatile bool _queryWorking;
    private bool _neighboursStopLogged;
    private bool _allBandsStopLogged;
    private (List<string> Calls, List<int> Dxccs) _hunt = ([], []);

    /// <summary>
    /// Creates the service. <paramref name="uploadsAllowed"/> is false in simulation: replayed recordings must never be
    /// reported as heard on the air.
    /// </summary>
    public PskReporterService(Session session, AppSettingsAccessor settings, SpotRepository spots, CountryFile countries, FrequencyTable frequencies, IClock clock,
                              bool uploadsAllowed)
        : this(session, settings, spots, countries, frequencies, clock, uploadsAllowed, null)
    {
    }

    /// <summary>Creates the service; <paramref name="newFeed"/> replaces the live feed's broker in tests.</summary>
    internal PskReporterService(Session session, AppSettingsAccessor settings, SpotRepository spots, CountryFile countries, FrequencyTable frequencies, IClock clock,
                                bool uploadsAllowed, Func<PskFeedClient>? newFeed)
    {
        _newFeed = newFeed ?? (() => new PskFeedClient(countries));
        _frequencies = frequencies;
        _session = session;
        _settings = settings;
        _spots = spots;
        _countries = countries;
        _clock = clock;
        _uploadsAllowed = uploadsAllowed;
        _uploader = new PskSpotUploader(SendAsync, clock);
    }

    /// <summary>Raised for each report of a station on the Hunt list (from any receiver; the caller filters by distance).</summary>
    public event Action<HuntSpot>? HuntReport;

    /// <summary>True while the live feed is running (Hunt list reports can arrive).</summary>
    public bool FeedRunning => _feed is not null;

    /// <summary>The callsign the feed, the query and spot uploads follow (empty when none).</summary>
    internal string FollowedCall => _myCall;

    /// <summary>Waits until the latest callsign or feed setting change has been applied (tests).</summary>
    internal Task Settled => _identity;

    /// <summary>True when Hunt list reports came too fast and following them was stopped.</summary>
    public bool HuntStopped => _feed?.HuntStopped == true;

    /// <summary>
    /// Follows the Hunt list on every band: every report of <paramref name="calls"/>, and reports of senders in the
    /// <paramref name="dxccs"/> (ADIF codes) heard by receivers in my DXCC entity.
    /// </summary>
    public void FollowHunt(IReadOnlyCollection<string> calls, IReadOnlyCollection<int> dxccs)
    {
        _hunt = ([.. calls], [.. dxccs]);
        var feed = _feed;
        feed?.FollowHunt(calls, dxccs, feed.MyDxcc);
    }

    /// <summary>
    /// Starts the uploader and, when the feed is on and a callsign is set, loads the last hour of stored reports and starts
    /// the feed and the fallback query. A later change of callsign (Setup on first run, a station profile switch) or of
    /// the feed setting restarts them for the new call.
    /// </summary>
    public void Start()
    {
        _settings.Changed += OnSettingsChanged;
        _session.BandChanged += FollowNeighbours;
        _session.SlotDecoded += OnSlotDecoded;
        _session.KeyboardHeard += OnKeyboardHeard;
        // PSK31, PSK63 and RTTY have no slots to catch up on, so follow a band or mode change when it happens.
        _session.BandChanged += FollowNeighbours;
        _ = Task.Run(() => UploadLoopAsync(_cts.Token));
        _identity = Task.Run(() => FollowIdentityAsync(_settings.Current));
        _identity.Wait();
    }

    // Starts, stops or restarts the feed and the query for the callsign and feed setting in use. Runs one at a time.
    private async Task FollowIdentityAsync(Ft8Client.Data.Settings.AppSettings s)
    {
        var call = Callsign.Normalize(s.Profile.Callsign);
        var on = call.Length > 0 && s.PskReporter.FeedEnabled;
        await _identityGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cts.IsCancellationRequested || (call == _myCall && on == (_feed is not null))) return;
            var previous = _myCall;
            if (previous.Length > 0 && previous != call)
            {
                // Spots waiting to go out were heard by the previous callsign: send them under it first.
                try
                {
                    await _uploader.FlushAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException or OperationCanceledException)
                {
                    Log.Warning(ex, "Could not send PSK Reporter spots before the callsign change");
                }
                _uploader.Receiver = null;
            }
            await StopFeedAsync().ConfigureAwait(false);
            _myCall = call;
            if (!on)
            {
                _session.SetReportsAvailable(false);
                _session.SetService(ServiceNames.Psk, ServiceStatus.Off("PSK Reporter off"));
                return;
            }
            if (previous.Length > 0) Log.Information("PSK Reporter now follows {Call}", call);
            _session.SetReportsAvailable(false);
            LoadStored();
            var feed = _newFeed();
            feed.Report += OnReport;
            feed.NeighbourReport += _session.AddNeighbourReport;
            feed.BandSampled += _session.AddBandSample;
            feed.HuntReport += spot => HuntReport?.Invoke(spot);
            feed.StatusChanged += st =>
            {
                if (!ReferenceEquals(feed, _feed)) return;
                _session.SetReportsAvailable(st.Health == ServiceHealth.Ok || _queryWorking);
                if (st.Health == ServiceHealth.Ok || !_queryWorking) _session.SetService(ServiceNames.Psk, st);
            };
            _session.SetService(ServiceNames.Psk, new ServiceStatus(ServiceHealth.Degraded, "Connecting to the live feed", _clock.UtcNow));
            _feed = feed;
            feed.Start(call);
            FollowNeighbours();
            feed.FollowHunt(_hunt.Calls, _hunt.Dxccs, feed.MyDxcc);
            _queryCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var token = _queryCts.Token;
            _ = Task.Run(() => QueryLoopAsync(call, token));
        }
        finally
        {
            _identityGate.Release();
        }
    }

    private async Task StopFeedAsync()
    {
        _queryWorking = false;
        if (_queryCts is not null)
        {
            await _queryCts.CancelAsync().ConfigureAwait(false);
            _queryCts.Dispose();
            _queryCts = null;
        }
        var feed = _feed;
        _feed = null;
        if (feed is not null) await feed.DisposeAsync().ConfigureAwait(false);
    }

    private void LoadStored()
    {
        try
        {
            _spots.Prune(_clock.UtcNow);
            foreach (var r in _spots.Since(_clock.UtcNow.AddHours(-1)))
            {
                if (!Callsign.EqualsCall(r.MyCall, _myCall) || r.Source == ReverseBeaconService.Source || r.Snr is not { } snr) continue;
                _session.AddReport(new ReceptionReport(r.RxCall, r.RxGrid, r.RxEntityKey, null, r.Band, r.Mode, snr, r.TimeUtc, r.FreqHz));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load stored reception reports");
        }
    }

    private void OnReport(ReceptionReport r)
    {
        Store(r, "mqtt");
        FollowNeighbours();
    }

    // Follows stations around the current grid in Settings, without waiting for a report of my signal: on
    // the current band for the neighbour comparison, on every band for the band menu (sampling the menu's bands in turn
    // when every band at once is too busy).
    private void FollowNeighbours()
    {
        var feed = _feed;
        if (feed is null) return;
        var settings = _settings.Current;
        var o = settings.PskReporter;
        var on = o.FeedEnabled && (o.CompareNeighbours || o.BandActivity);
        // The configuration, not the snapshot: on a band change the snapshot is published after BandChanged is raised.
        var config = _session.Config;
        feed.FollowNeighbours(on ? config.Band : null, on ? ModeInfo.Name(config.Mode) : null, on ? settings.Profile.Grid : null, o.BandActivity,
            _frequencies.Bands(config.Mode));
        _session.SetBandActivityFeed(!o.FeedEnabled || !o.BandActivity ? BandActivityFeed.Off
            : Grid.ToLatLon(settings.Profile.Grid) is null ? BandActivityFeed.WaitingForGrid
            : feed.NeighboursStopped ? BandActivityFeed.Stopped
            : feed.AllBandsStopped ? BandActivityFeed.CurrentBandOnly
            : BandActivityFeed.AllBands);
        feed.FollowHunt(_hunt.Calls, _hunt.Dxccs, feed.MyDxcc);
        if (feed.AllBandsStopped && !_allBandsStopLogged)
        {
            _allBandsStopLogged = true;
            Log.Warning("PSK Reporter reports on every band came too fast; following the current band and sampling the others");
        }
        if (feed.NeighboursStopped && !_neighboursStopLogged)
        {
            _neighboursStopLogged = true;
            Log.Warning("PSK Reporter neighbour reports came too fast; stopped following them and sampling every band");
        }
    }

    private void OnSettingsChanged(Ft8Client.Data.Settings.AppSettings s)
    {
        var call = Callsign.Normalize(s.Profile.Callsign);
        var on = call.Length > 0 && s.PskReporter.FeedEnabled;
        if (call != _myCall || on != (_feed is not null))
        {
            _identity = _identity.ContinueWith(_ => FollowIdentityAsync(s), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
            return;
        }
        FollowNeighbours();
    }

    private void Store(ReceptionReport r, string source)
    {
        try
        {
            var added = _spots.Add(new SpotRecord
            {
                MyCall = _myCall, RxCall = r.ReceiverCall, RxGrid = r.ReceiverGrid, RxEntityKey = r.ReceiverEntityKey, Band = r.Band,
                Mode = r.Mode, FreqHz = r.FrequencyHz, Snr = r.Snr, TimeUtc = r.TimeUtc, Source = source,
            });
            if (added) _session.AddReport(r);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not store a reception report");
            _session.AddReport(r);
        }
    }

    private async Task QueryLoopAsync(string myCall, CancellationToken ct)
    {
        using var http = HttpFactory.Create(myCall);
        var query = new PskQueryClient(http, _countries);
        // Give the feed a moment so a working feed does not also cost a query.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        while (!ct.IsCancellationRequested)
        {
            if (_feed?.Connected != true)
            {
                var reports = await query.QueryAsync(myCall, _settings.Current.PskReporter.AppContact, _clock.UtcNow, ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested) return;
                if (reports is null)
                {
                    _queryWorking = false;
                    _session.SetReportsAvailable(false);
                    _session.SetService(ServiceNames.Psk, new ServiceStatus(ServiceHealth.Down, "PSK Reporter unreachable", _clock.UtcNow));
                }
                else
                {
                    _queryWorking = true;
                    foreach (var r in reports) Store(r, "query");
                    _session.SetReportsAvailable(true);
                    _session.SetService(ServiceNames.Psk, new ServiceStatus(ServiceHealth.Degraded, "Live feed down; reports every 5 minutes", _clock.UtcNow));
                }
            }
            try
            {
                await Task.Delay(PskQueryClient.MinInterval + TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnSlotDecoded(DateTime slot, Mode mode, IReadOnlyList<Decode> decodes)
    {
        // Band, mode and the setting can change at any time; catching up once a slot is soon enough.
        FollowNeighbours();
        if (UploadDial() is not { } dial) return;
        foreach (var d in decodes)
        {
            if (d.LowConfidence) continue;
            var m = MessageParser.Parse(d.Text);
            if (m.SenderCall is not { } call) continue;
            _uploader.Add(new PskSpot(call, dial + d.OffsetHz, d.Snr, ModeInfo.Name(mode), m.Grid ?? string.Empty, slot));
        }
    }

    // PSK31, PSK63 and RTTY stations named from their text, spotted like fldigi does: the ADIF mode name and the dial plus
    // the channel's audio frequency (V23). The uploader's once-per-call rule keeps a long over to one spot.
    private void OnKeyboardHeard(Mode mode, IReadOnlyList<TextHeard> heard)
    {
        if (UploadDial() is not { } dial) return;
        foreach (var h in heard) _uploader.Add(new PskSpot(h.Call, dial + h.OffsetHz, h.Snr, ModeInfo.Name(mode), string.Empty, h.Utc));
    }

    // The dial to spot against, with my station set on the uploader; null when spots are not to be uploaded.
    private long? UploadDial()
    {
        var s = _settings.Current;
        if (!_uploadsAllowed || !s.PskReporter.UploadSpots || _myCall.Length == 0) return null;
        var dial = _session.Snapshot.DialHz;
        if (dial <= 0) return null;
        _uploader.Receiver = new PskReceiver(_myCall, s.Profile.Grid, $"{AppInfo.ProductName} {AppInfo.Version}");
        return dial;
    }

    private async Task UploadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                await _uploader.TickAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PSK Reporter spot upload failed");
            }
        }
    }

    private async Task SendAsync(byte[] payload)
    {
        _udp ??= new UdpClient(AddressFamily.InterNetwork);
        await _udp.SendAsync(payload, UploadHost, UploadPort, _cts.Token).ConfigureAwait(false);
    }

    // Spots still waiting go out when Costas closes, as WSJT-X sends its pending spots when it stops. Never longer than
    // a couple of seconds: closing must not hang on the network.
    private async Task FlushOnExitAsync()
    {
        if (_uploader.Pending == 0) return;
        try
        {
            await _uploader.FlushAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or OperationCanceledException or TimeoutException)
        {
            Log.Information("PSK Reporter spots not sent at exit: {Error}", ex.Message);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _settings.Changed -= OnSettingsChanged;
        _session.BandChanged -= FollowNeighbours;
        _session.SlotDecoded -= OnSlotDecoded;
        _session.KeyboardHeard -= OnKeyboardHeard;
        await FlushOnExitAsync().ConfigureAwait(false);
        await _cts.CancelAsync().ConfigureAwait(false);
        await _identityGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopFeedAsync().ConfigureAwait(false);
        }
        finally
        {
            _identityGate.Release();
        }
        _udp?.Dispose();
        _cts.Dispose();
    }
}
