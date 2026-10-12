// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Buffers;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using MQTTnet;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>
/// The live "who hears me" feed from mqtt.pskreporter.info (a best-effort community service by M0LTE). Subscribes to
/// <c>pskr/filter/v2/+/+/MYCALL/#</c>, reconnects with back-off, and drops repeated sequence numbers. When asked, it also
/// follows the reports of every station in my DXCC entity on one band and mode, for the neighbour comparison.
/// </summary>
public sealed class PskFeedClient : IAsyncDisposable
{
    /// <summary>The public broker.</summary>
    public const string DefaultHost = "mqtt.pskreporter.info";

    /// <summary>TLS port.</summary>
    public const int DefaultTlsPort = 1884;

    private readonly string _host;
    private readonly int _port;
    private readonly bool _tls;
    private readonly CountryFile? _countries;
    private readonly HashSet<long> _seen = [];
    private readonly Queue<long> _seenOrder = new();
    private readonly Backoff _backoff = Backoff.Network();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private IMqttClient? _client;
    private volatile bool _subscribed;
    private readonly object _topicGate = new();
    private string? _neighbourTopic;
    private string? _subscribedNeighbourTopic;
    private (string? Band, string? Mode, int? Dxcc) _followed;
    private int _neighbourCount;
    private DateTime _neighbourMinute;

    /// <summary>More neighbour reports than this in one minute stops following them (the feed is a shared community service).</summary>
    public const int MaxNeighbourReportsPerMinute = 6000;

    /// <summary>Creates a client.</summary>
    public PskFeedClient(CountryFile? countries, string host = DefaultHost, int port = DefaultTlsPort, bool tls = true)
    {
        _countries = countries;
        _host = host;
        _port = port;
        _tls = tls;
    }

    /// <summary>Raised for each new report of my signal.</summary>
    public event Action<ReceptionReport>? Report;

    /// <summary>Raised for each report of another station on the band followed by <see cref="FollowNeighbours"/>.</summary>
    public event Action<NeighbourReport>? NeighbourReport;

    /// <summary>My ADIF DXCC code, learned from the first report of my signal that carries one.</summary>
    public int? MyDxcc { get; private set; }

    /// <summary>True when neighbour reports came too fast and following them was stopped.</summary>
    public bool NeighboursStopped { get; private set; }

    /// <summary>
    /// True when neighbour reports on every band came too fast; the feed then follows the current band only.
    /// </summary>
    public bool AllBandsStopped { get; private set; }

    /// <summary>Raised when the connection state changes.</summary>
    public event Action<ServiceStatus>? StatusChanged;

    /// <summary>True while connected and subscribed (reports can arrive).</summary>
    public bool Connected => _subscribed && _client?.IsConnected == true;

    /// <summary>Connections made so far (reconnects included).</summary>
    public int Connections { get; private set; }

    /// <summary>The topic filter for a call.</summary>
    public static string TopicFor(string call) => $"pskr/filter/v2/+/+/{Callsign.Normalize(call)}/#";

    /// <summary>The topic filter for every sender in one DXCC entity on one band (<c>+</c> for every band) and mode.</summary>
    public static string NeighbourTopicFor(string band, string mode, int dxcc) =>
        $"pskr/filter/v2/{band.ToLowerInvariant()}/{mode.ToUpperInvariant()}/+/+/+/+/{dxcc}/#";

    /// <summary>
    /// Follows the reports of stations in DXCC entity <paramref name="dxcc"/> on <paramref name="band"/> and
    /// <paramref name="mode"/>, replacing any earlier choice; null for any of them stops following. With
    /// <paramref name="allBands"/> it follows every band (for the band menu), unless that proved too busy.
    /// </summary>
    public void FollowNeighbours(string? band, string? mode, int? dxcc, bool allBands = false)
    {
        var topic = band is null || mode is null || dxcc is null || NeighboursStopped ? null
            : NeighbourTopicFor(allBands && !AllBandsStopped ? "+" : band, mode, dxcc.Value);
        lock (_topicGate)
        {
            _followed = (band, mode, dxcc);
            if (topic == _neighbourTopic) return;
            _neighbourTopic = topic;
        }
        _ = UpdateNeighbourSubscriptionAsync();
    }

    private async Task UpdateNeighbourSubscriptionAsync()
    {
        var client = _client;
        if (client is null || !_subscribed) return;
        string? want, have;
        lock (_topicGate)
        {
            want = _neighbourTopic;
            have = _subscribedNeighbourTopic;
            if (want == have) return;
            _subscribedNeighbourTopic = want;
        }
        try
        {
            if (have is not null) await client.UnsubscribeAsync(have).ConfigureAwait(false);
            if (want is not null) await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(want).Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The next reconnect subscribes again.
            lock (_topicGate) _subscribedNeighbourTopic = null;
        }
    }

    /// <summary>Starts following reports of <paramref name="myCall"/>.</summary>
    public void Start(string myCall)
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(Callsign.Normalize(myCall), _cts.Token));
    }

    private async Task RunAsync(string myCall, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var disconnected = new TaskCompletionSource();
            try
            {
                _client = new MqttClientFactory().CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += e =>
                {
                    OnMessage(e.ApplicationMessage.Payload, myCall);
                    return Task.CompletedTask;
                };
                _client.DisconnectedAsync += _ =>
                {
                    _subscribed = false;
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_host, _port)
                    .WithTlsOptions(o => o.UseTls(_tls))
                    .WithClientId($"costas-{Guid.NewGuid():N}"[..23])
                    .WithCleanSession(true)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                    .WithTimeout(TimeSpan.FromSeconds(15))
                    .Build();
                await _client.ConnectAsync(options, ct).ConfigureAwait(false);
                await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(TopicFor(myCall)).Build(), ct).ConfigureAwait(false);
                Connections++;
                lock (_topicGate) _subscribedNeighbourTopic = null;
                _subscribed = true;
                await UpdateNeighbourSubscriptionAsync().ConfigureAwait(false);
                _backoff.Reset();
                StatusChanged?.Invoke(new ServiceStatus(ServiceHealth.Ok, "Live feed connected", DateTime.UtcNow));
                await disconnected.Task.WaitAsync(ct).ConfigureAwait(false);
                StatusChanged?.Invoke(new ServiceStatus(ServiceHealth.Degraded, "Live feed lost; reconnecting", DateTime.UtcNow));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(new ServiceStatus(ServiceHealth.Down, $"Live feed unavailable: {ex.Message}", DateTime.UtcNow));
            }
            finally
            {
                _subscribed = false;
                _client?.Dispose();
                _client = null;
            }
            try
            {
                await Task.Delay(_backoff.NextDelay(), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnMessage(ReadOnlySequence<byte> payload, string myCall)
    {
        var parsed = PskReportParser.ParseMqtt(payload.ToArray(), _countries);
        if (parsed is not { } p) return;
        var mine = Callsign.EqualsCall(p.SenderCall, myCall);
        if (!mine && (_neighbourTopic is null || p.SenderGrid is null)) return;
        lock (_seen)
        {
            if (p.Sequence != 0)
            {
                if (!_seen.Add(p.Sequence)) return;
                _seenOrder.Enqueue(p.Sequence);
                if (_seenOrder.Count > 5000) _seen.Remove(_seenOrder.Dequeue());
            }
        }
        if (mine)
        {
            MyDxcc ??= p.SenderDxcc;
            Report?.Invoke(p.Report);
            return;
        }
        if (TooManyNeighbourReports()) return;
        NeighbourReport?.Invoke(new NeighbourReport(p.SenderCall, p.SenderGrid!, p.Report.ReceiverCall, p.Report.Band, p.Report.Mode, p.Report.Snr,
            p.Report.TimeUtc));
    }

    private bool TooManyNeighbourReports()
    {
        var minute = DateTime.UtcNow;
        minute = new DateTime(minute.Year, minute.Month, minute.Day, minute.Hour, minute.Minute, 0, DateTimeKind.Utc);
        if (minute != _neighbourMinute)
        {
            _neighbourMinute = minute;
            _neighbourCount = 0;
        }
        if (++_neighbourCount <= MaxNeighbourReportsPerMinute) return false;
        string? topic;
        lock (_topicGate) topic = _neighbourTopic;
        if (topic is not null && topic.StartsWith("pskr/filter/v2/+/", StringComparison.Ordinal))
        {
            // Every band is too busy: fall back to the current band, which the neighbour comparison needs.
            AllBandsStopped = true;
            var (band, mode, dxcc) = _followed;
            FollowNeighbours(band, mode, dxcc);
        }
        else if (!NeighboursStopped)
        {
            NeighboursStopped = true;
            FollowNeighbours(null, null, null);
        }
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_loop is not null) await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _cts.Dispose();
    }
}
