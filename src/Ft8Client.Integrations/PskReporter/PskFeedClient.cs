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
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using MQTTnet;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>
/// The live "who hears me" feed from mqtt.pskreporter.info (a best-effort community service by M0LTE). Subscribes to
/// <c>pskr/filter/v2/+/+/MYCALL/#</c>, reconnects with back-off, and drops repeated sequence numbers.
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

    /// <summary>Raised when the connection state changes.</summary>
    public event Action<ServiceStatus>? StatusChanged;

    /// <summary>True while connected and subscribed (reports can arrive).</summary>
    public bool Connected => _subscribed && _client?.IsConnected == true;

    /// <summary>Connections made so far (reconnects included).</summary>
    public int Connections { get; private set; }

    /// <summary>The topic filter for a call.</summary>
    public static string TopicFor(string call) => $"pskr/filter/v2/+/+/{Callsign.Normalize(call)}/#";

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
                _subscribed = true;
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
        if (parsed is not { } p || !Callsign.EqualsCall(p.SenderCall, myCall)) return;
        lock (_seen)
        {
            if (p.Sequence != 0)
            {
                if (!_seen.Add(p.Sequence)) return;
                _seenOrder.Enqueue(p.Sequence);
                if (_seenOrder.Count > 5000) _seen.Remove(_seenOrder.Dequeue());
            }
        }
        Report?.Invoke(p.Report);
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
