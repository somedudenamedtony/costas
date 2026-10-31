// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;
using Ft8Client.Audio.Dsp;
using Ft8Client.Core;
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Time;
using Ft8Client.Modems;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.App.Engine;

/// <summary>
/// Reads the keyboard modes (docs/09-keyboard-modes.md section 12 step 5) beside <see cref="Session"/>: it drains the
/// continuous 12 kHz audio, reads every PSK signal in it, works out whose text each channel carries and hands the
/// stations and finished lines to the session. Audio captured while Costas is transmitting is dropped.
/// </summary>
public sealed class KeyboardSession : IDisposable
{
    // Lines for Raw decodes end at a new line, at the end of an over, at this length, or after a pause.
    private const int LineLength = 80;
    private static readonly TimeSpan LinePause = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AgeEvery = TimeSpan.FromSeconds(1);

    // A channel with no callsign counts as a signal while it has had text this recently (a station keeps its channel 60 s).
    private static readonly TimeSpan UnnamedFor = TimeSpan.FromSeconds(60);

    // Shown when the spectrum has no measure of a signal yet (the floor of the dB scale used for FT8).
    private const int NoSnr = -24;

    private readonly Session _session;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly float[] _buffer = new float[ModeInfo.DecoderSampleRate];
    private readonly Dictionary<int, Reading> _channels = [];
    private Mode _mode = Mode.Ft8;
    private RttyFormat _rtty = RttyFormat.Standard;
    private bool _unshiftOnSpace = true;
    private bool _afc = true;
    private ChannelBank? _bank;
    private int? _listening;
    private DateTime _agedUtc;
    private Thread? _thread;
    private volatile bool _run;

    /// <summary>Creates a reader for a session.</summary>
    public KeyboardSession(Session session, IClock clock)
    {
        _session = session;
        _clock = clock;
        _session.BandChanged += () => SetMode(_session.Config.Mode);
        SetMode(session.Config.Mode);
    }

    /// <summary>The continuous audio to read, written by the capture pump (eight seconds of room).</summary>
    public SpscRingBuffer Input { get; } = new(8 * ModeInfo.DecoderSampleRate);

    /// <summary>
    /// Follows the session's band and mode: starts afresh reading PSK31, PSK63 or RTTY, or reads nothing in a slotted
    /// mode.
    /// </summary>
    public void SetMode(Mode mode)
    {
        lock (_gate)
        {
            _mode = mode;
            Restart();
        }
    }

    /// <summary>The RTTY speed, shift and polarity to read, and unshift-on-space. Starts afresh when they change in RTTY.</summary>
    public void SetRtty(RttyFormat format, bool unshiftOnSpace)
    {
        lock (_gate)
        {
            if (format == _rtty && unshiftOnSpace == _unshiftOnSpace) return;
            _rtty = format;
            _unshiftOnSpace = unshiftOnSpace;
            if (_mode == Mode.Rtty) Restart();
        }
    }

    /// <summary>Lets channels follow drift (true, the default) or holds each on its frequency. Applies to channels being read.</summary>
    public void SetAfc(bool on)
    {
        lock (_gate)
        {
            _afc = on;
            _bank?.Afc = on;
        }
    }

    private void Restart()
    {
        _bank = _mode switch
        {
            Mode.Psk31 or Mode.Psk63 => new ChannelBank(_mode, ModeInfo.DecoderSampleRate),
            Mode.Rtty => new ChannelBank(_rtty, ModeInfo.DecoderSampleRate, _unshiftOnSpace),
            _ => null,
        };
        _bank?.Afc = _afc;
        _channels.Clear();
        _listening = null;
        Input.Clear();
    }

    /// <summary>Reads the signal at an audio frequency the operator clicked, and keeps reading it while it is quiet.</summary>
    public void Listen(int hz)
    {
        lock (_gate)
        {
            if (_bank is null) return;
            // Only the latest click is kept read through a quiet spell.
            if (_listening is { } old) _bank.Release(old);
            _listening = _bank.Listen(hz).Id;
        }
    }

    /// <summary>Starts reading on its own thread.</summary>
    public void Start()
    {
        _run = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "KeyboardSession" };
        _thread.Start();
    }

    private void Loop()
    {
        while (_run)
        {
            Drain();
            Thread.Sleep(100);
        }
    }

    /// <summary>Reads whatever audio is waiting and passes on what it held. Called by the reader thread; public for tests.</summary>
    public void Drain()
    {
        var heard = new List<TextHeard>();
        var lines = new List<KeyboardLine>();
        Mode mode;
        IReadOnlyList<int> unnamed;
        lock (_gate)
        {
            mode = _mode;
            int n;
            // While transmitting, the receiver hears only my own signal (or nothing): it is dropped, not read.
            var transmitting = _session.IsTransmitting;
            while ((n = Input.Read(_buffer)) > 0)
            {
                if (_bank is null || transmitting) continue;
                Read(_bank, _buffer.AsSpan(0, n), heard, lines);
            }
            if (_bank is null) return;
            var now = _clock.UtcNow;
            foreach (var (id, c) in _channels.ToList())
            {
                if (c.Line.Length > 0 && now - c.LastUtc > LinePause) Flush(id, now, lines);
                if (!_bank.Channels.Any(x => x.Id == id) && c.Line.Length == 0) _channels.Remove(id);
            }
            if (heard.Count == 0 && lines.Count == 0 && now - _agedUtc < AgeEvery) return;
            _agedUtc = now;
            // Signals with text but no callsign yet: shown under Next in line and tagged "?" on the waterfall.
            unnamed = [.. _channels.Where(kv => kv.Value.Channel.Call is null && now - kv.Value.LastUtc <= UnnamedFor)
                .Select(kv => _bank.Channels.FirstOrDefault(x => x.Id == kv.Key))
                .OfType<ReceiveChannel>()
                .Select(c => (int)Math.Round(c.Receiver.FrequencyHz))
                .Order()];
        }
        _session.ApplyText(mode, heard, lines, _clock.UtcNow, unnamed);
    }

    private void Read(ChannelBank bank, ReadOnlySpan<float> samples, List<TextHeard> heard, List<KeyboardLine> lines)
    {
        var now = _clock.UtcNow;
        foreach (var t in bank.Process(samples))
        {
            if (!_channels.TryGetValue(t.Channel, out var c)) _channels[t.Channel] = c = new Reading();
            var channel = bank.Channels.FirstOrDefault(x => x.Id == t.Channel);
            var snr = channel is null ? null : bank.SnrDb(channel);
            var offset = (int)Math.Round(t.FrequencyHz);
            var h = c.Channel.Add(t.Text, now, offset, snr ?? NoSnr);
            if (h is not null)
            {
                heard.Add(h);
                c.To = h.To;
            }
            c.LastUtc = now;
            foreach (var ch in t.Text)
            {
                if (ch is '\n' or '\r')
                {
                    Flush(t.Channel, now, lines, offset, snr);
                    continue;
                }
                c.Line.Append(ch);
                if (c.Line.Length >= LineLength) Flush(t.Channel, now, lines, offset, snr);
            }
            if (h is { OverEnded: true }) Flush(t.Channel, now, lines, offset, snr);
        }
    }

    private void Flush(int id, DateTime now, List<KeyboardLine> lines, int? offset = null, int? snr = null)
    {
        var c = _channels[id];
        var text = c.Line.ToString().Trim();
        c.Line.Clear();
        if (text.Length == 0) return;
        var channel = _bank?.Channels.FirstOrDefault(x => x.Id == id);
        var hz = offset ?? (channel is null ? 0 : (int)Math.Round(channel.Receiver.FrequencyHz));
        var db = snr ?? (channel is null ? null : _bank!.SnrDb(channel)) ?? NoSnr;
        lines.Add(new KeyboardLine(now, hz, db, text, c.Channel.Call, c.To));
    }

    // What one channel has read: its station, the line being built for Raw decodes, and when it last had text.
    private sealed class Reading
    {
        public TextChannel Channel { get; } = new();
        public StringBuilder Line { get; } = new();
        public string? To { get; set; }
        public DateTime LastUtc { get; set; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
    }
}
