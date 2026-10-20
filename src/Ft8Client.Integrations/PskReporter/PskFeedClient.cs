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
/// follows the reports of every station in my DXCC entity on one band and mode, for the neighbour comparison, and on every
/// band for the band menu; when every band is too busy to follow at once, it samples the other bands one at a time.
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
    private (string? Band, string? Mode, int? Dxcc, bool AllBands, IReadOnlyList<string> Bands) _followed = (null, null, null, false, []);
    private bool _neighbourAllBands;
    private readonly BandSampler _sampler;
    private string? _sampleTopic;
    private string? _subscribedSampleTopic;
    private Timer? _sampleTimer;
    private HashSet<string> _huntTopics = new(StringComparer.Ordinal);
    private readonly HashSet<string> _subscribedHuntTopics = new(StringComparer.Ordinal);
    private HashSet<string> _huntCalls = new(StringComparer.Ordinal);
    private HashSet<int> _huntDxccs = [];
    private int _huntCount;
    private DateTime _huntMinute;
    private int _neighbourCount;
    private DateTime _neighbourMinute;

    /// <summary>More neighbour reports than this in one minute stops following them (the feed is a shared community service).</summary>
    public const int MaxNeighbourReportsPerMinute = 6000;

    /// <summary>Creates a client.</summary>
    /// <param name="countries">For the receivers' countries.</param>
    /// <param name="host">The broker.</param>
    /// <param name="port">Its port.</param>
    /// <param name="tls">True to use TLS.</param>
    /// <param name="sampleDwell">How long each band is sampled (<see cref="BandSampler.DefaultDwell"/>; shorter in tests).</param>
    public PskFeedClient(CountryFile? countries, string host = DefaultHost, int port = DefaultTlsPort, bool tls = true, TimeSpan? sampleDwell = null)
    {
        _countries = countries;
        _sampler = new BandSampler(sampleDwell ?? BandSampler.DefaultDwell);
        _host = host;
        _port = port;
        _tls = tls;
    }

    /// <summary>Raised for each new report of my signal.</summary>
    public event Action<ReceptionReport>? Report;

    /// <summary>Raised for each report of another station on the band followed by <see cref="FollowNeighbours"/>.</summary>
    public event Action<NeighbourReport>? NeighbourReport;

    /// <summary>
    /// Raised each time a band finishes being sampled, while the bands are too busy to follow all at once (see
    /// <see cref="FollowNeighbours"/>). Raised on a timer thread.
    /// </summary>
    public event Action<BandSample>? BandSampled;

    /// <summary>
    /// My ADIF DXCC code, learned from the first report of my signal that carries one, or remembered from an earlier run.
    /// </summary>
    public int? MyDxcc { get; private set; }

    /// <summary>Uses a DXCC code remembered from an earlier run until a report of my signal gives one.</summary>
    public void RememberMyDxcc(int dxcc) => MyDxcc ??= dxcc;

    /// <summary>True when neighbour reports came too fast and following them was stopped.</summary>
    public bool NeighboursStopped { get; private set; }

    /// <summary>
    /// True when neighbour reports on every band came too fast; the feed then follows the current band and samples the others.
    /// </summary>
    public bool AllBandsStopped { get; private set; }

    /// <summary>Raised for each report of a station on the Hunt list (see <see cref="FollowHunt"/>), heard by anyone.</summary>
    public event Action<HuntSpot>? HuntReport;

    /// <summary>More Hunt list reports than this in one minute stops following them (the feed is a shared community service).</summary>
    public const int MaxHuntReportsPerMinute = 1200;

    /// <summary>True when Hunt list reports came too fast and following them was stopped.</summary>
    public bool HuntStopped { get; private set; }

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

    /// <summary>The topic filter for every report of one sender, on any band and mode.</summary>
    public static string HuntCallTopicFor(string call) => $"pskr/filter/v2/+/+/{Callsign.Normalize(call)}/#";

    /// <summary>
    /// The topic filter for senders in DXCC entity <paramref name="dxcc"/> heard by receivers in <paramref name="receiverDxcc"/>
    /// (any receiver when null), on any band and mode.
    /// </summary>
    public static string HuntEntityTopicFor(int dxcc, int? receiverDxcc) =>
        $"pskr/filter/v2/+/+/+/+/+/+/{dxcc}/{(receiverDxcc is { } r ? r.ToString(System.Globalization.CultureInfo.InvariantCulture) : "+")}/#";

    /// <summary>
    /// Follows the Hunt list on every band: every report of <paramref name="calls"/>, and reports of senders in the DXCC
    /// entities <paramref name="dxccs"/> heard in <paramref name="receiverDxcc"/>. Replaces any earlier list; empty lists stop.
    /// </summary>
    public void FollowHunt(IEnumerable<string> calls, IEnumerable<int> dxccs, int? receiverDxcc)
    {
        var callSet = new HashSet<string>(calls.Select(Callsign.Normalize), StringComparer.Ordinal);
        var dxccSet = new HashSet<int>(dxccs);
        var topics = HuntStopped ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(callSet.Select(HuntCallTopicFor).Concat(dxccSet.Select(d => HuntEntityTopicFor(d, receiverDxcc))), StringComparer.Ordinal);
        lock (_topicGate)
        {
            _huntCalls = callSet;
            _huntDxccs = dxccSet;
            if (topics.SetEquals(_huntTopics)) return;
            _huntTopics = topics;
        }
        _ = UpdateHuntSubscriptionsAsync();
    }

    private async Task UpdateHuntSubscriptionsAsync()
    {
        var client = _client;
        if (client is null || !_subscribed) return;
        List<string> add, remove;
        lock (_topicGate)
        {
            add = _huntTopics.Where(t => !_subscribedHuntTopics.Contains(t)).ToList();
            remove = _subscribedHuntTopics.Where(t => !_huntTopics.Contains(t)).ToList();
            _subscribedHuntTopics.ExceptWith(remove);
            _subscribedHuntTopics.UnionWith(add);
        }
        try
        {
            foreach (var t in remove) await client.UnsubscribeAsync(t).ConfigureAwait(false);
            foreach (var t in add) await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(t).Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The next reconnect subscribes again.
            lock (_topicGate) _subscribedHuntTopics.Clear();
        }
    }

    /// <summary>
    /// Follows the reports of stations in DXCC entity <paramref name="dxcc"/> on <paramref name="band"/> and
    /// <paramref name="mode"/>, replacing any earlier choice; null for any of them stops following. With
    /// <paramref name="allBands"/> it follows every band (for the band menu); when that proves too busy it follows
    /// <paramref name="band"/> and samples the other <paramref name="bands"/> one at a time (<see cref="BandSampled"/>).
    /// </summary>
    /// <param name="band">The band I am on.</param>
    /// <param name="mode"><c>FT8</c> or <c>FT4</c>.</param>
    /// <param name="dxcc">My ADIF DXCC code.</param>
    /// <param name="allBands">True to follow every band.</param>
    /// <param name="bands">The bands to sample when every band is too busy to follow; null or empty for none.</param>
    public void FollowNeighbours(string? band, string? mode, int? dxcc, bool allBands = false, IReadOnlyList<string>? bands = null)
    {
        var wide = allBands && !AllBandsStopped;
        var topic = band is null || mode is null || dxcc is null || NeighboursStopped ? null
            : NeighbourTopicFor(wide ? "+" : band, mode, dxcc.Value);
        lock (_topicGate)
        {
            _followed = (band, mode, dxcc, allBands, bands ?? []);
            _neighbourAllBands = wide;
            if (topic == _neighbourTopic) return;
            _neighbourTopic = topic;
        }
        _ = UpdateNeighbourSubscriptionAsync();
    }

    // Once a second: moves the band sampling on while every band is too busy to follow at once.
    private void SampleTick()
    {
        BandSample? finished = null;
        bool resubscribe;
        lock (_topicGate)
        {
            var (band, mode, dxcc, allBands, bands) = _followed;
            if (allBands && mode is not null && dxcc is not null && bands.Count > 0 && (AllBandsStopped || NeighboursStopped))
            {
                var m = mode.ToUpperInvariant();
                (finished, _) = _sampler.Tick(DateTime.UtcNow, bands, NeighboursStopped ? null : band, m);
                _sampleTopic = _sampler.Band is { } b ? NeighbourTopicFor(b, m, dxcc.Value) : null;
            }
            else
            {
                _sampler.Stop();
                _sampleTopic = null;
            }
            resubscribe = _sampleTopic != _subscribedSampleTopic;
        }
        if (resubscribe) _ = UpdateSampleSubscriptionAsync();
        if (finished is not null) BandSampled?.Invoke(finished);
    }

    private async Task UpdateSampleSubscriptionAsync()
    {
        var client = _client;
        if (client is null || !_subscribed) return;
        string? want, have;
        lock (_topicGate)
        {
            want = _sampleTopic;
            have = _subscribedSampleTopic;
            if (want == have) return;
            _subscribedSampleTopic = want;
        }
        try
        {
            if (have is not null) await client.UnsubscribeAsync(have).ConfigureAwait(false);
            if (want is not null) await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(want).Build()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The next tick or reconnect subscribes again.
            lock (_topicGate) _subscribedSampleTopic = null;
        }
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
        _sampleTimer = new Timer(_ => SampleTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
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
                lock (_topicGate)
                {
                    _subscribedNeighbourTopic = null;
                    _subscribedSampleTopic = null;
                    _subscribedHuntTopics.Clear();
                }
                _subscribed = true;
                await UpdateNeighbourSubscriptionAsync().ConfigureAwait(false);
                await UpdateSampleSubscriptionAsync().ConfigureAwait(false);
                await UpdateHuntSubscriptionsAsync().ConfigureAwait(false);
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
        var hunted = !mine && IsHunted(p.SenderCall, p.SenderDxcc);
        bool neighbour, sampled;
        lock (_topicGate)
        {
            var (band, _, dxcc, _, _) = _followed;
            neighbour = !mine && _neighbourTopic is not null && p.SenderGrid is not null
                        && (_neighbourAllBands || string.Equals(p.Report.Band, band, StringComparison.OrdinalIgnoreCase));
            // The sampled band's reports only feed the band menu's estimate, not the neighbour comparison or its limit.
            sampled = !mine && _sampler.Band is { } sb && string.Equals(p.Report.Band, sb, StringComparison.OrdinalIgnoreCase) && p.SenderDxcc == dxcc;
        }
        if (!mine && !hunted && !neighbour && !sampled) return;
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
            if (p.SenderDxcc is { } dxcc) MyDxcc = dxcc;
            Report?.Invoke(p.Report);
            return;
        }
        if (hunted && !TooManyHuntReports()) HuntReport?.Invoke(new HuntSpot(p.SenderCall, p.SenderGrid, p.SenderDxcc, p.Report));
        if (sampled)
        {
            var r = p.SenderGrid is null ? null
                : new NeighbourReport(p.SenderCall, p.SenderGrid, p.Report.ReceiverCall, p.Report.Band, p.Report.Mode, p.Report.Snr, p.Report.TimeUtc);
            lock (_topicGate) _sampler.Add(r);
            return;
        }
        if (!neighbour || TooManyNeighbourReports()) return;
        NeighbourReport?.Invoke(new NeighbourReport(p.SenderCall, p.SenderGrid!, p.Report.ReceiverCall, p.Report.Band, p.Report.Mode, p.Report.Snr,
            p.Report.TimeUtc));
    }

    private bool IsHunted(string call, int? dxcc)
    {
        lock (_topicGate)
        {
            if (_huntTopics.Count == 0) return false;
            return _huntCalls.Contains(Callsign.Normalize(call)) || _huntCalls.Contains(Callsign.BaseCall(call)) || (dxcc is { } d && _huntDxccs.Contains(d));
        }
    }

    private bool TooManyHuntReports()
    {
        var now = DateTime.UtcNow;
        var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        if (minute != _huntMinute)
        {
            _huntMinute = minute;
            _huntCount = 0;
        }
        if (++_huntCount <= MaxHuntReportsPerMinute) return false;
        if (!HuntStopped)
        {
            HuntStopped = true;
            FollowHunt([], [], null);
        }
        return true;
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
            // Every band is too busy: fall back to the current band, which the neighbour comparison needs. The current band
            // starts a fresh count, or its next report would stop following altogether.
            AllBandsStopped = true;
            _neighbourCount = 0;
            var (band, mode, dxcc, allBands, bands) = _followed;
            FollowNeighbours(band, mode, dxcc, allBands, bands);
        }
        else if (!NeighboursStopped)
        {
            // Following stops; the band menu goes on sampling every band.
            NeighboursStopped = true;
            var (band, mode, dxcc, allBands, bands) = _followed;
            FollowNeighbours(band, mode, dxcc, allBands, bands);
        }
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_cts is null) return;
        if (_sampleTimer is not null) await _sampleTimer.DisposeAsync().ConfigureAwait(false);
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
