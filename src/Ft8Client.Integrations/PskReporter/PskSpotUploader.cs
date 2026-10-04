// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>
/// Collects spots and sends them to report.pskreporter.info:4739 over UDP: at most one packet every five minutes
/// (unless one fills), not aligned to the clock (random jitter, timed from start), templates in the first three packets
/// and then hourly, each callsign at most once per five minutes, never my own call. Off until the operator enables it.
/// </summary>
public sealed class PskSpotUploader
{
    /// <summary>Minimum interval between sends.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PerCall = TimeSpan.FromMinutes(5);
    private readonly object _gate = new();
    private readonly Func<byte[], Task> _send;
    private readonly IClock _clock;
    private readonly Random _rng;
    private readonly List<PskSpot> _queue = [];
    private readonly Dictionary<string, DateTime> _lastSpotted = new(StringComparer.Ordinal);
    private readonly uint _observationId;
    private uint _sequence;
    private int _descriptorsLeft = 3;
    private DateTime _nextSend;
    private DateTime _nextDescriptors;

    /// <summary>Creates an uploader that hands datagrams to <paramref name="send"/>.</summary>
    public PskSpotUploader(Func<byte[], Task> send, IClock clock, Random? rng = null)
    {
        _send = send;
        _clock = clock;
        _rng = rng ?? Random.Shared;
        _observationId = (uint)_rng.Next();
        _nextSend = clock.UtcNow + Interval + Jitter();
        _nextDescriptors = clock.UtcNow + TimeSpan.FromHours(1);
    }

    /// <summary>My station.</summary>
    public PskReceiver? Receiver { get; set; }

    /// <summary>Spots waiting.</summary>
    public int Pending
    {
        get { lock (_gate) return _queue.Count; }
    }

    /// <summary>Adds a spot unless it is my call, unresolved, or the same call was spotted in the last five minutes.</summary>
    public bool Add(PskSpot spot)
    {
        var call = Callsign.Normalize(spot.Callsign);
        var me = Receiver?.Callsign;
        if (!Callsign.IsValid(call) || Callsign.EqualsCall(call, me)) return false;
        lock (_gate)
        {
            if (_lastSpotted.TryGetValue(call, out var t) && spot.TimeUtc - t < PerCall) return false;
            _lastSpotted[call] = spot.TimeUtc;
            _queue.Add(spot with { Callsign = call });
            return true;
        }
    }

    /// <summary>Sends if the interval has passed (or a packet would be full). Call every few seconds.</summary>
    public async Task<int> TickAsync()
    {
        List<PskSpot> spots;
        bool descriptors;
        uint seq;
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (Receiver is null) return 0;
            var full = _queue.Count >= 60;
            if (now < _nextSend && !full) return 0;
            if (_queue.Count == 0 && _descriptorsLeft == 0 && now < _nextDescriptors)
            {
                _nextSend = now + Interval + Jitter();
                return 0;
            }
            spots = [.. _queue];
            _queue.Clear();
            if (now >= _nextDescriptors)
            {
                _descriptorsLeft = Math.Max(_descriptorsLeft, 1);
                _nextDescriptors = now + TimeSpan.FromHours(1);
            }
            descriptors = _descriptorsLeft > 0;
            if (descriptors) _descriptorsLeft--;
            seq = _sequence;
            _nextSend = now + Interval + Jitter();
            foreach (var (k, t) in _lastSpotted.ToList()) if (now - t > PerCall) _lastSpotted.Remove(k);
        }
        var packets = IpfixPacketBuilder.Build(Receiver!, spots, descriptors, seq, _observationId,
            (uint)new DateTimeOffset(now).ToUnixTimeSeconds());
        foreach (var p in packets)
        {
            await _send(p.Payload).ConfigureAwait(false);
            lock (_gate) _sequence += (uint)p.SpotCount;
        }
        return packets.Count;
    }

    private TimeSpan Jitter() => TimeSpan.FromSeconds(_rng.Next(0, 30));
}
