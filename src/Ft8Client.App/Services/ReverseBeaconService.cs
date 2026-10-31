// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.Concurrent;
using Ft8Client.App.Engine;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;
using Ft8Client.Data.Settings;
using Ft8Client.Integrations.ReverseBeacon;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// The Reverse Beacon Network in the background: its skimmers' spots of my callsign join PSK Reporter's reports in the
/// Reach view, the globe, the countries line and the Hears you column. Both telnet relays are followed (CW and RTTY on
/// 7000, FT8 and FT4 on 7001); spots in modes Costas does not send (CW) are dropped. A skimmer heard by both networks in
/// the same five minutes on the same band counts once. Optional: nothing needs it to operate.
/// </summary>
public sealed class ReverseBeaconService : IAsyncDisposable
{
    /// <summary>The <see cref="SpotRecord.Source"/> of stored RBN spots.</summary>
    public const string Source = "rbn";

    // The modes Costas sends, as RBN names them.
    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal) { "FT8", "FT4", "RTTY", "PSK31", "PSK63" };

    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly SpotRepository _spots;
    private readonly CallsignCacheRepository _cache;
    private readonly CountryFile _countries;
    private readonly IClock _clock;
    private readonly Func<int, RbnFeedClient> _newFeed;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, string?> _grids = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, ServiceStatus> _status = new();
    private readonly List<RbnFeedClient> _feeds = [];
    private Task _identity = Task.CompletedTask;
    private string _myCall = string.Empty;
    private bool _disposed;

    /// <summary>Creates the service; <paramref name="newFeed"/> (given a relay port) replaces the relays in tests.</summary>
    public ReverseBeaconService(Session session, AppSettingsAccessor settings, SpotRepository spots, CallsignCacheRepository cache, CountryFile countries,
                                IClock clock, Func<int, RbnFeedClient>? newFeed = null)
    {
        _session = session;
        _settings = settings;
        _spots = spots;
        _cache = cache;
        _countries = countries;
        _clock = clock;
        _newFeed = newFeed ?? (port => new RbnFeedClient(RbnFeedClient.DefaultHost, port, () => clock.UtcNow));
    }

    /// <summary>The callsign followed (empty when off).</summary>
    internal string FollowedCall => _myCall;

    /// <summary>Waits until the latest callsign or setting change has been applied (tests).</summary>
    internal Task Settled => _identity;

    /// <summary>Starts following my callsign when the setting is on; later changes of callsign or setting restart it.</summary>
    public void Start()
    {
        _settings.Changed += OnSettingsChanged;
        _identity = Task.Run(() => FollowAsync(_settings.Current));
        _identity.Wait();
    }

    private void OnSettingsChanged(AppSettings s) =>
        _identity = _identity.ContinueWith(_ => FollowAsync(s), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();

    private async Task FollowAsync(AppSettings s)
    {
        var on = s.ReverseBeacon.Enabled ? Callsign.Normalize(s.Profile.Callsign) : string.Empty;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || (on == _myCall && (on.Length > 0) == (_feeds.Count > 0))) return;
            await StopAsync().ConfigureAwait(false);
            _myCall = on;
            if (on.Length == 0)
            {
                _session.SetService(ServiceNames.Rbn, ServiceStatus.Off("Reverse Beacon Network off"));
                return;
            }
            LoadStored();
            _session.SetService(ServiceNames.Rbn, new ServiceStatus(ServiceHealth.Degraded, "Connecting", _clock.UtcNow));
            foreach (var port in new[] { RbnFeedClient.DigitalPort, RbnFeedClient.CwRttyPort })
            {
                var feed = _newFeed(port);
                _status[port] = new ServiceStatus(ServiceHealth.Degraded, "Connecting", _clock.UtcNow);
                feed.Spot += OnSpot;
                feed.StatusChanged += st => OnStatus(feed, port, st);
                lock (_feeds) _feeds.Add(feed);
                feed.Start(on);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopAsync()
    {
        List<RbnFeedClient> feeds;
        lock (_feeds)
        {
            feeds = [.. _feeds];
            _feeds.Clear();
        }
        _status.Clear();
        foreach (var f in feeds) await f.DisposeAsync().ConfigureAwait(false);
        _session.SetReportsAvailable(false, ServiceNames.Rbn);
    }

    // One status for both relays: working while either is (the other one is named while it is not).
    private void OnStatus(RbnFeedClient feed, int port, ServiceStatus st)
    {
        lock (_feeds)
        {
            if (!_feeds.Contains(feed)) return;
        }
        _status[port] = st;
        var digital = _status.GetValueOrDefault(RbnFeedClient.DigitalPort);
        var cwRtty = _status.GetValueOrDefault(RbnFeedClient.CwRttyPort);
        var up = (digital?.Health == ServiceHealth.Ok ? 1 : 0) + (cwRtty?.Health == ServiceHealth.Ok ? 1 : 0);
        _session.SetReportsAvailable(up > 0, ServiceNames.Rbn);
        var now = _clock.UtcNow;
        _session.SetService(ServiceNames.Rbn, up switch
        {
            2 => new ServiceStatus(ServiceHealth.Ok, "Connected", now),
            1 when digital?.Health != ServiceHealth.Ok => new ServiceStatus(ServiceHealth.Ok, $"RTTY spots connected; FT8/FT4 spots: {digital?.Message}", now),
            1 => new ServiceStatus(ServiceHealth.Ok, $"FT8/FT4 spots connected; RTTY spots: {cwRtty?.Message}", now),
            _ => st,
        });
    }

    private void OnSpot(RbnSpot spot)
    {
        if (!Modes.Contains(spot.Mode) || BandPlan.BandOf(spot.FrequencyHz) is not { } band) return;
        var lookup = LookupCall(spot.Skimmer);
        var report = new ReceptionReport(spot.Skimmer, GridOf(lookup), _countries.Lookup(lookup)?.Entity.Key, null, band.ToLowerInvariant(), spot.Mode,
            spot.Snr, spot.TimeUtc, spot.FrequencyHz);
        try
        {
            var added = _spots.Add(new SpotRecord
            {
                MyCall = _myCall, RxCall = report.ReceiverCall, RxGrid = report.ReceiverGrid, RxEntityKey = report.ReceiverEntityKey, Band = report.Band,
                Mode = report.Mode, FreqHz = report.FrequencyHz, Snr = report.Snr, TimeUtc = report.TimeUtc, Source = Source,
            });
            if (added) _session.AddReport(report);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not store a Reverse Beacon Network spot");
            _session.AddReport(report);
        }
    }

    // A second skimmer at one station is named W3LPL-2; the station is W3LPL.
    internal static string LookupCall(string skimmer)
    {
        var dash = skimmer.LastIndexOf('-');
        return dash > 0 && skimmer[(dash + 1)..].All(char.IsDigit) ? skimmer[..dash] : skimmer;
    }

    // RBN gives no locator. Use one already known without asking anyone: a cached QRZ lookup, or the skimmer's own
    // PSK Reporter reports (many skimmers report to both). Otherwise the map places it in its country.
    private string? GridOf(string call) => _grids.GetOrAdd(call, c =>
    {
        try
        {
            var cached = _cache.GetFresh(c, _clock.UtcNow)?.Grid;
            return Grid.ToLatLon(cached) is not null ? cached : _spots.LatestGrid(c);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not look up a skimmer's locator");
            return null;
        }
    });

    private void LoadStored()
    {
        try
        {
            foreach (var r in _spots.Since(_clock.UtcNow.AddHours(-1)))
            {
                if (r.Source != Source || !Callsign.EqualsCall(r.MyCall, _myCall) || r.Snr is not { } snr) continue;
                _session.AddReport(new ReceptionReport(r.RxCall, r.RxGrid, r.RxEntityKey, null, r.Band, r.Mode, snr, r.TimeUtc, r.FreqHz));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load stored Reverse Beacon Network spots");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _settings.Changed -= OnSettingsChanged;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
