// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;
using Ft8Client.Audio.Backends;
using Ft8Client.Core;
using Ft8Client.Core.Time;
using Ft8Client.Modems;

namespace Ft8Client.App.Engine;

/// <summary>
/// Sends PSK and RTTY as it is typed (docs/09-keyboard-modes.md sections 8 and 9). Text is queued and a reader
/// thread turns it into audio a little ahead of the radio, through <see cref="Transmitter.StartStream"/>, so its guards
/// (band edge, radio and audio faults, Halt) apply. Characters the mode cannot send are refused with the reason before
/// anything is queued. With nothing left to send, the over either closes and returns to receive (after a macro) or
/// idles until more is typed. The keyboard watchdog closes an over after 3 minutes on the air or 20 seconds of idle.
/// </summary>
public sealed class KeyboardTransmitter : IDisposable
{
    /// <summary>Longest over before the watchdog closes it.</summary>
    public static readonly TimeSpan MaxOver = TimeSpan.FromMinutes(3);

    /// <summary>Longest idle with nothing to send before the watchdog closes the over.</summary>
    public static readonly TimeSpan MaxIdle = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Past <see cref="MaxOver"/> by this much the over has not closed by itself (the audio device stopped taking
    /// samples, say), so PTT is dropped at once through Halt.
    /// </summary>
    public static readonly TimeSpan StallGrace = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The audio device has taken no samples for this long: it has stalled, so PTT is dropped through the transmitter's
    /// fault path rather than holding the radio keyed until <see cref="MaxOver"/>.
    /// </summary>
    public static readonly TimeSpan AudioStall = TimeSpan.FromSeconds(5);

    /// <summary>Shown when the audio device stops taking samples during an over.</summary>
    public const string AudioStallFault = "Audio output stopped taking samples during the over; PTT released. Check the sound card.";

    // The transmitter dropped the over without telling us for this long: end it here so the keyboard is not stuck.
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromSeconds(1);

    // Audio rendered ahead of the radio: enough to ride out a busy moment on the reader thread, little enough that
    // Backspace can still take back what was just typed.
    private const double AheadSeconds = 0.3;

    private readonly Transmitter _tx;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly StringBuilder _waiting = new();
    private readonly StringBuilder _sent = new();
    private readonly List<(int End, int Key)> _macroEnds = [];
    private readonly List<(int Key, long EndSample)> _macrosSent = [];
    private IKeyboardModulator? _mod;
    private AudioStream? _stream;
    private Thread? _thread;
    private bool _endAfter;
    private bool _closing;
    private bool _watchdog;
    private bool _idleClosed;
    private bool _starting;
    private bool _startCut;
    private long _written;
    private long _played;
    private DateTime _playedAtUtc;
    private DateTime? _orphanSinceUtc;
    private volatile bool _disposed;
    private int _sentChars;
    private DateTime _overStartUtc;
    private DateTime _lastActivityUtc;

    private readonly bool _background;

    /// <summary>
    /// Creates a keyboard transmitter over the station's transmitter. With <paramref name="background"/> false no reader
    /// thread is started and the caller renders audio with <see cref="Pump"/> (tests do this on a manual clock).
    /// </summary>
    public KeyboardTransmitter(Transmitter tx, IClock clock, bool background = true)
    {
        _tx = tx;
        _clock = clock;
        _background = background;
        _tx.StreamEnded += OnStreamEnded;
    }

    /// <summary>Raised when what is sent or queued changes, on the thread that changed it.</summary>
    public event Action? Changed;

    /// <summary>Raised when an over ends, with what went out.</summary>
    public event Action<KeyboardOver>? OverEnded;

    /// <summary>True while an over is on the air or starting.</summary>
    public bool Active
    {
        get { lock (_gate) return _stream is not null; }
    }

    /// <summary>The state for the screen.</summary>
    public KeyboardTxView View
    {
        get
        {
            lock (_gate)
            {
                return _stream is null && _waiting.Length == 0
                    ? KeyboardTxView.Idle
                    : new KeyboardTxView(_stream is not null, _sent.ToString(), _waiting.ToString(), _endAfter);
            }
        }
    }

    /// <summary>
    /// The characters in <paramref name="text"/> that the modulator cannot send, after it normalises them (RTTY sends
    /// capitals); empty when all can go.
    /// </summary>
    public static IReadOnlyList<char> Unsendable(IKeyboardModulator mod, string text)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Select(mod.Normalize).Where(c => !mod.CanSend(c)).Distinct()];
    }

    /// <summary>
    /// Queues text, starting an over if none is on the air. <paramref name="macroKey"/> marks the text as that macro, so
    /// the over reports it sent once it has gone out whole. With <paramref name="returnToReceive"/> the over closes when
    /// everything queued has gone. Returns null, or why nothing was queued (a character the mode cannot send, or a
    /// transmit guard). <paramref name="start"/> makes the modulator and starts the over when none is on the air.
    /// </summary>
    public string? Send(string text, int? macroKey, bool returnToReceive, Func<IKeyboardModulator> modulator, Func<AudioStream, string?> start)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(modulator);
        ArgumentNullException.ThrowIfNull(start);
        lock (_gate)
        {
            var mod = _mod ?? modulator();
            var bad = Unsendable(mod, text);
            if (bad.Count > 0) return $"Not sent: this mode cannot send {string.Join(' ', bad.Select(Show))}. Remove {(bad.Count == 1 ? "it" : "them")} and send again.";
            if (_closing) return "The over is closing; send again once it has ended.";
            var normalised = new string([.. text.Select(mod.Normalize)]);
            if (_stream is null)
            {
                var stream = new AudioStream(_tx.OutputSampleRate * 4);
                // The over is ours before the transmitter keys the radio: a PTT failure inside start() ends it at once,
                // and that end must find it (otherwise the keyboard would wait for an over that never comes).
                _stream = stream;
                _starting = true;
                _startCut = false;
                string? why;
                try
                {
                    why = start(stream);
                }
                finally
                {
                    _starting = false;
                }
                if (why is null && (_startCut || !_tx.IsStreaming(stream))) why = "The over was stopped before it started.";
                if (why is not null)
                {
                    _stream = null;
                    _startCut = false;
                    return why;
                }
                _mod = mod;
                _sent.Clear();
                _sentChars = 0;
                _written = 0;
                _macroEnds.Clear();
                _macrosSent.Clear();
                _watchdog = false;
                _idleClosed = false;
                _overStartUtc = _clock.UtcNow;
                _played = 0;
                _playedAtUtc = _overStartUtc;
                _orphanSinceUtc = null;
                Write(mod.Start());
                if (_background)
                {
                    _thread = new Thread(Run) { IsBackground = true, Name = "Keyboard transmit" };
                    _thread.Start();
                }
            }
            _waiting.Append(normalised);
            if (macroKey is { } k) _macroEnds.Add((_sentChars + _waiting.Length, k));
            _endAfter = returnToReceive;
            _lastActivityUtc = _clock.UtcNow;
        }
        Changed?.Invoke();
        return null;
    }

    /// <summary>Adds one typed character to an over on the air. Returns false (and queues nothing) when it cannot be sent or no over is on the air.</summary>
    public bool Type(char c)
    {
        lock (_gate)
        {
            if (_stream is null || _mod is null || _closing) return false;
            c = _mod.Normalize(c);
            if (!_mod.CanSend(c)) return false;
            _waiting.Append(c);
            _endAfter = false;
            _lastActivityUtc = _clock.UtcNow;
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes the last character not yet sent. False when everything typed has already gone out.</summary>
    public bool Backspace()
    {
        lock (_gate)
        {
            if (_waiting.Length == 0) return false;
            _waiting.Length--;
            _macroEnds.RemoveAll(m => m.End > _sentChars + _waiting.Length);
            _lastActivityUtc = _clock.UtcNow;
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Closes the over once everything queued has gone (Enter on an empty line).</summary>
    public void ReturnToReceive()
    {
        lock (_gate)
        {
            if (_stream is null) return;
            _endAfter = true;
        }
        Changed?.Invoke();
    }

    /// <summary>Drops text not yet sent (Esc and Halt stop the audio through the transmitter).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _waiting.Clear();
            _macroEnds.RemoveAll(m => m.End > _sentChars);
        }
        Changed?.Invoke();
    }

    // Reader thread: keeps the stream a little ahead of the radio until the over has ended.
    private void Run()
    {
        while (Pump()) Thread.Sleep(20);
    }

    /// <summary>
    /// Renders audio until the stream is a little ahead of the radio, applying the watchdog. An over that has not ended
    /// <see cref="StallGrace"/> after the watchdog limit is halted. Returns false once the over has ended. The reader
    /// thread calls it; tests call it directly.
    /// </summary>
    public bool Pump()
    {
        var changed = false;
        var stalled = false;
        var audioStalled = false;
        var orphan = false;
        lock (_gate)
        {
            if (_disposed || _stream is not { } stream || _mod is not { } mod) return false;
            var now = _clock.UtcNow;
            if (stream.Played != _played)
            {
                _played = stream.Played;
                _playedAtUtc = now;
            }
            if (_tx.IsStreaming(stream)) _orphanSinceUtc = null;
            else _orphanSinceUtc ??= now;
            if (_orphanSinceUtc is { } since && now - since >= OrphanGrace)
            {
                orphan = true;
            }
            else if (now - _overStartUtc >= MaxOver + StallGrace)
            {
                _watchdog = true;
                stalled = true;
            }
            else if (now - _playedAtUtc >= AudioStall)
            {
                // The device took nothing for seconds (unplugged, driver hung): the radio must not stay keyed.
                audioStalled = true;
            }
            else
            {
                var ahead = (int)(AheadSeconds * _tx.OutputSampleRate);
                while (stream.Buffered < ahead && !_closing)
                {
                    var idle = _waiting.Length == 0 && !_endAfter && now - _lastActivityUtc >= MaxIdle;
                    if (now - _overStartUtc >= MaxOver || idle)
                    {
                        _watchdog = true;
                        _idleClosed = idle && now - _overStartUtc < MaxOver;
                        _waiting.Clear();
                        Close(mod, stream);
                    }
                    else if (_waiting.Length > 0)
                    {
                        var c = _waiting[0];
                        _waiting.Remove(0, 1);
                        Write(mod.Send(c));
                        _sent.Append(c);
                        _sentChars++;
                        // Counted sent only once its last sample has played (a Halt can still cut what is buffered).
                        foreach (var m in _macroEnds.Where(m => m.End == _sentChars)) _macrosSent.Add((m.Key, _written));
                        _lastActivityUtc = now;
                        changed = true;
                    }
                    else if (_endAfter)
                    {
                        Close(mod, stream);
                    }
                    else
                    {
                        Write(mod.Idle());
                    }
                }
            }
        }
        if (orphan)
        {
            EndOver(false);
            return false;
        }
        if (stalled)
        {
            _ = _tx.HaltAsync();
            return false;
        }
        if (audioStalled)
        {
            _ = _tx.AbortAsync(AudioStallFault);
            return false;
        }
        if (changed) Changed?.Invoke();
        return true;
    }

    // Under _gate.
    private void Close(IKeyboardModulator mod, AudioStream stream)
    {
        Write(mod.Stop());
        stream.Complete();
        _closing = true;
    }

    // Under _gate. The stream holds four seconds; the reader keeps it well below that.
    private void Write(float[] audio)
    {
        var level = Math.Pow(10, _tx.GainDb / 20);
        var scaled = new float[audio.Length];
        for (var i = 0; i < audio.Length; i++) scaled[i] = (float)(audio[i] * level);
        _written += _stream!.Write(scaled);
    }

    /// <summary>
    /// Ends an over the transmitter is no longer sending (after Halt, a band change or a fault while it was starting),
    /// so the keyboard never waits for an over that will not end by itself. Does nothing while the over is on the air.
    /// </summary>
    public void EndIfNotOnAir()
    {
        lock (_gate)
        {
            if (_stream is not { } s || _starting || _tx.IsStreaming(s)) return;
        }
        EndOver(false);
    }

    /// <summary>True while the macro on <paramref name="key"/> is queued and has not all gone out yet.</summary>
    public bool MacroWaiting(int key)
    {
        lock (_gate) return _stream is not null && _macroEnds.Any(m => m.Key == key && m.End > _sentChars);
    }

    private void OnStreamEnded(bool completed) => EndOver(completed);

    private void EndOver(bool completed)
    {
        KeyboardOver over;
        lock (_gate)
        {
            if (_stream is not { } stream) return;
            if (_starting)
            {
                // Ended while the transmitter was still starting it (PTT failed): Send reports why; nothing went out.
                _startCut = true;
                return;
            }
            var played = stream.Played;
            int[] macros = [.. _macrosSent.Where(m => completed || m.EndSample <= played).Select(m => m.Key)];
            over = new KeyboardOver(_sent.ToString(), macros, completed && !_watchdog, _watchdog) { Idle = _idleClosed };
            _stream = null;
            _mod = null;
            _closing = false;
            _waiting.Clear();
            _macroEnds.Clear();
            _endAfter = false;
        }
        OverEnded?.Invoke(over);
        Changed?.Invoke();
    }

    private static string Show(char c) => c switch
    {
        '\t' => "tab",
        _ when char.IsControl(c) => $"U+{(int)c:X4}",
        _ => $"'{c}'",
    };

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _thread?.Join(500);
    }
}
