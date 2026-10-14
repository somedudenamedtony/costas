// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core;
using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.Modems;

/// <summary>
/// Reads every PSK or RTTY signal in the passband at once (docs/09-keyboard-modes.md section 5): the channel finder
/// looks for new signals twice a second, each gets its own receiver, and a channel is dropped after
/// <see cref="QuietSeconds"/> without text unless the operator pinned it. Two channels that AFC brings onto the same
/// signal are merged. Not thread-safe; feed it from one thread.
/// </summary>
public sealed class ChannelBank
{
    private readonly int _sampleRate;
    private readonly IChannelFinder _finder;
    private readonly Func<double, IKeyboardReceiver> _receiver;
    private readonly double _widthHz;
    private readonly double _snapHz;
    private readonly List<ReceiveChannel> _channels = [];
    // The last few seconds of audio: a channel the finder opens reads them first, so the start of the over it found
    // (usually the caller's call) is not lost while the signal was still being noticed.
    private readonly float[] _history;
    private int _historyPos;
    private bool _historyFull;
    private long _samples;
    private long _nextScan;
    private int _nextId = 1;

    /// <summary>A bank reading PSK31 or PSK63.</summary>
    public ChannelBank(Mode mode, int sampleRate)
        : this(new ChannelFinder(mode, sampleRate), hz => new PskDemodulator(mode, sampleRate, hz), PskMode.Baud(mode), 2 * PskMode.Baud(mode), sampleRate)
    {
    }

    /// <summary>
    /// A bank reading RTTY in one speed, shift and polarity. A click anywhere on a signal, either tone or between them,
    /// reads it, so the operator need not find its centre.
    /// </summary>
    public ChannelBank(RttyFormat format, int sampleRate, bool unshiftOnSpace = true)
        : this(
            new RttyChannelFinder(format, sampleRate),
            hz => new RttyDemodulator(format, sampleRate, hz) { UnshiftOnSpace = unshiftOnSpace },
            Math.Max(format.Baud, format.ShiftHz / 4.0),
            format.ShiftHz / 2.0 + 2 * format.Baud,
            sampleRate)
    {
    }

    /// <param name="finder">Finds signals.</param>
    /// <param name="receiver">Makes a receiver tuned to a frequency.</param>
    /// <param name="widthHz">Two channels closer than this are on the same signal; a new one is not opened within twice it.</param>
    /// <param name="snapHz">A frequency the operator picks this close to a signal reads that signal.</param>
    /// <param name="sampleRate">Input sample rate.</param>
    public ChannelBank(IChannelFinder finder, Func<double, IKeyboardReceiver> receiver, double widthHz, double snapHz, int sampleRate)
    {
        _finder = finder;
        _receiver = receiver;
        _widthHz = widthHz;
        _snapHz = snapHz;
        _sampleRate = sampleRate;
        _history = new float[HistorySeconds * sampleRate];
    }

    /// <summary>Seconds of audio a new channel reads back.</summary>
    public const int HistorySeconds = 4;

    /// <summary>Most signals read at once.</summary>
    public int MaxChannels { get; init; } = 20;

    /// <summary>Seconds without text after which an unpinned channel is dropped.</summary>
    public double QuietSeconds { get; init; } = 60;

    /// <summary>The channels being read, lowest frequency first.</summary>
    public IReadOnlyList<ReceiveChannel> Channels => [.. _channels.OrderBy(c => c.Receiver.FrequencyHz)];

    /// <summary>A channel's SNR in dB in 2,500 Hz (see <see cref="IChannelFinder.SnrDb"/>), or null when unknown.</summary>
    public int? SnrDb(ReceiveChannel channel) => _finder.SnrDb(channel.Receiver.FrequencyHz);

    /// <summary>Starts reading a signal at a frequency the operator picked (or returns the channel already on it) and pins it.</summary>
    public ReceiveChannel Listen(double frequencyHz)
    {
        var channel = _channels
            .Where(c => Math.Abs(c.Receiver.FrequencyHz - frequencyHz) <= _snapHz)
            .MinBy(c => Math.Abs(c.Receiver.FrequencyHz - frequencyHz))
            ?? Add(Nearest(_finder.Signals(), frequencyHz) ?? frequencyHz);
        channel.Pinned = true;
        return channel;
    }

    /// <summary>Unpins a channel, so it is dropped when it goes quiet.</summary>
    public void Release(int id)
    {
        if (_channels.Find(c => c.Id == id) is { } c) c.Pinned = false;
    }

    /// <summary>Feeds audio; returns the text each channel completed in it.</summary>
    public IReadOnlyList<ChannelText> Process(ReadOnlySpan<float> samples)
    {
        _finder.Process(samples);
        List<ChannelText>? texts = null;
        if (_backfill.Count > 0)
        {
            texts = [.. _backfill];
            _backfill.Clear();
        }
        foreach (var c in _channels)
        {
            var t = c.Receiver.Process(samples);
            if (t.Length == 0) continue;
            c.LastTextSample = _samples + samples.Length;
            (texts ??= []).Add(new ChannelText(c.Id, c.Receiver.FrequencyHz, t));
        }
        Remember(samples);
        _samples += samples.Length;
        if (_samples >= _nextScan)
        {
            _nextScan = _samples + _sampleRate / 2;
            Scan();
        }
        return texts ?? (IReadOnlyList<ChannelText>)[];
    }

    private void Scan()
    {
        var quiet = (long)(QuietSeconds * _sampleRate);
        _channels.RemoveAll(c => !c.Pinned && _samples - c.LastTextSample > quiet);

        // Merge channels that AFC has brought onto one signal: keep the older (or pinned) one.
        var width = _widthHz;
        foreach (var c in _channels.OrderByDescending(c => c.Id).ToList())
        {
            if (!c.Pinned && _channels.Exists(o => o != c && o.Id < c.Id && Math.Abs(o.Receiver.FrequencyHz - c.Receiver.FrequencyHz) < width))
                _channels.Remove(c);
        }

        foreach (var hz in _finder.Signals())
        {
            if (_channels.Count >= MaxChannels) break;
            if (Near(hz) is not null) continue;
            var c = Add(hz);
            var text = c.Receiver.Process(History());
            if (text.Length == 0) continue;
            c.LastTextSample = _samples;
            _backfill.Add(new ChannelText(c.Id, c.Receiver.FrequencyHz, text));
        }
    }

    private readonly List<ChannelText> _backfill = [];

    private void Remember(ReadOnlySpan<float> samples)
    {
        foreach (var x in samples.Length > _history.Length ? samples[^_history.Length..] : samples)
        {
            _history[_historyPos++] = x;
            if (_historyPos < _history.Length) continue;
            _historyPos = 0;
            _historyFull = true;
        }
    }

    private float[] History() => _historyFull ? [.. _history[_historyPos..], .. _history[.._historyPos]] : _history[.._historyPos];

    private double? Nearest(IReadOnlyList<double> frequencies, double hz)
    {
        double? best = null;
        foreach (var f in frequencies)
        {
            if (Math.Abs(f - hz) <= _snapHz && (best is null || Math.Abs(f - hz) < Math.Abs(best.Value - hz))) best = f;
        }
        return best;
    }

    private ReceiveChannel? Near(double hz) => _channels.Find(c => Math.Abs(c.Receiver.FrequencyHz - hz) < 2 * _widthHz);

    private ReceiveChannel Add(double hz)
    {
        var channel = new ReceiveChannel(_nextId++, _receiver(hz), _samples);
        _channels.Add(channel);
        return channel;
    }
}
