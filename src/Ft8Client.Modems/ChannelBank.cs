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
    private bool _afc = true;

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
            format.ShiftHz / 2.0 + format.Baud,
            sampleRate)
    {
    }

    /// <param name="finder">Finds signals.</param>
    /// <param name="receiver">Makes a receiver tuned to a frequency.</param>
    /// <param name="widthHz">Two channels closer than this are on the same signal; a new one is not opened within 1.2 times it.</param>
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

    /// <summary>
    /// True (the default) to let every channel follow its signal's drift; false holds each on its frequency, for a crowded
    /// band where AFC would pull a channel onto a stronger neighbour.
    /// </summary>
    public bool Afc
    {
        get => _afc;
        set
        {
            _afc = value;
            foreach (var c in _channels) c.Receiver.Afc = value;
        }
    }

    /// <summary>The channels being read, lowest frequency first.</summary>
    public IReadOnlyList<ReceiveChannel> Channels => [.. _channels.OrderBy(c => c.Receiver.FrequencyHz)];

    /// <summary>A channel's SNR in dB in 2,500 Hz (see <see cref="IChannelFinder.SnrDb"/>), or null when unknown.</summary>
    public int? SnrDb(ReceiveChannel channel) => _finder.SnrDb(channel.Receiver.FrequencyHz);

    /// <summary>Starts reading a signal at a frequency the operator picked (or returns the channel already on it) and pins it.</summary>
    public ReceiveChannel Listen(double frequencyHz)
    {
        // The signal clicked is the one the finder sees nearest the click; a channel already on it reads it. A quiet
        // channel near it is moved onto it; otherwise a channel opens there. With no signal seen, the nearest channel
        // within reach of the click is taken, else one opens at the click.
        ReceiveChannel channel;
        if (Nearest(_finder.Signals(), frequencyHz) is { } signal)
        {
            var idle = Quiet(signal);
            channel = Near(signal) ?? (idle is null ? Add(signal) : Retune(idle, signal));
        }
        else
        {
            channel = _channels
                .Where(c => Math.Abs(c.Receiver.FrequencyHz - frequencyHz) <= _snapHz)
                .MinBy(c => Math.Abs(c.Receiver.FrequencyHz - frequencyHz))
                ?? Add(frequencyHz);
        }
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

        // A signal is acted on once two looks in a row (half a second apart) find it in the same place, so the
        // passing shapes of the spectrum while signals come and go do not open channels or move them.
        var previous = _lastSignals;
        _lastSignals = _finder.Signals();

        // A channel that keeps printing while the finder sees no signal on it is reading a neighbour's edge or a
        // carrier, not a station: it is dropped (unless pinned). A quiet channel stays its usual time.
        foreach (var c in _channels)
        {
            if (_lastSignals.Any(hz => Math.Abs(hz - c.Receiver.FrequencyHz) < 1.2 * _widthHz)) c.AbsentSample = null;
            else c.AbsentSample ??= _samples;
        }
        _channels.RemoveAll(c => !c.Pinned && c.AbsentSample is { } absent
            && _samples - absent >= AbsentSeconds * _sampleRate && c.LastTextSample > absent);
        foreach (var hz in _lastSignals)
        {
            if (!previous.Any(p => Math.Abs(p - hz) <= SteadyHz * _widthHz)) continue;
            if (Near(hz) is { } near)
            {
                // A channel gone quiet a little off a signal is deaf to it: its receiver holds where the last signal was,
                // and AFC reaches only a few hertz. It is moved onto the signal (a reply a few hertz off, say).
                if (IsQuiet(near) && Math.Abs(near.Receiver.FrequencyHz - hz) > RetuneHz * _widthHz) Retune(near, hz);
                continue;
            }
            if (Quiet(hz) is { } idle)
            {
                Retune(idle, hz);
                continue;
            }
            if (_channels.Count >= MaxChannels) continue;
            var c = Add(hz);
            var text = c.Receiver.Process(History());
            if (text.Length == 0) continue;
            c.LastTextSample = _samples;
            _backfill.Add(new ChannelText(c.Id, c.Receiver.FrequencyHz, text));
        }
    }

    // A quiet channel is moved onto a signal this far off it (in signal widths), so AFC need not reach it.
    private const double RetuneHz = 0.08;

    // A channel without text for this long, whose receiver hears nothing and has had time to lock, is quiet.
    private const double QuietRetuneSeconds = 1;
    private const double LockSeconds = 2;

    private bool IsQuiet(ReceiveChannel c) =>
        _samples - c.LastTextSample >= QuietRetuneSeconds * _sampleRate
        && _samples - c.TunedSample >= LockSeconds * _sampleRate
        && c.Receiver.Quality < 0.3;

    // The nearest quiet channel within reach of a signal that no channel is reading.
    private ReceiveChannel? Quiet(double hz) => _channels
        .Where(c => Math.Abs(c.Receiver.FrequencyHz - hz) <= _snapHz && IsQuiet(c))
        .MinBy(c => Math.Abs(c.Receiver.FrequencyHz - hz));

    // Gives a channel a new receiver on a signal, keeping its number (and pin), and reads the audio since its last text.
    private ReceiveChannel Retune(ReceiveChannel channel, double hz)
    {
        channel.Receiver = _receiver(hz);
        channel.Receiver.Afc = _afc;
        channel.TunedSample = _samples;
        var history = History();
        var since = (int)Math.Min(history.Length, _samples - channel.LastTextSample);
        var text = channel.Receiver.Process(history.AsSpan(history.Length - since));
        if (text.Length > 0)
        {
            channel.LastTextSample = _samples;
            _backfill.Add(new ChannelText(channel.Id, channel.Receiver.FrequencyHz, text));
        }
        return channel;
    }

    private readonly List<ChannelText> _backfill = [];
    private IReadOnlyList<double> _lastSignals = [];

    // Seconds a channel may print with no signal under it before it is dropped.
    private const double AbsentSeconds = 5;

    // How far a signal may move between two looks and still be the same one, in signal widths.
    private const double SteadyHz = 0.1;

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

    // A channel reading the signal at hz. Closer than 1.2 widths: two PSK31 stations 40 Hz apart get a channel each.
    private ReceiveChannel? Near(double hz) => _channels
        .Where(c => Math.Abs(c.Receiver.FrequencyHz - hz) < 1.2 * _widthHz)
        .MinBy(c => Math.Abs(c.Receiver.FrequencyHz - hz));

    private ReceiveChannel Add(double hz)
    {
        var receiver = _receiver(hz);
        receiver.Afc = _afc;
        var channel = new ReceiveChannel(_nextId++, receiver, _samples);
        _channels.Add(channel);
        return channel;
    }
}
