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

    // Audio rendered ahead of the radio: enough to ride out a busy moment on the reader thread, little enough that
    // Backspace can still take back what was just typed.
    private const double AheadSeconds = 0.3;

    private readonly Transmitter _tx;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly StringBuilder _waiting = new();
    private readonly StringBuilder _sent = new();
    private readonly List<(int End, int Key)> _macroEnds = [];
    private readonly List<int> _macrosSent = [];
    private IKeyboardModulator? _mod;
    private AudioStream? _stream;
    private Thread? _thread;
    private bool _endAfter;
    private bool _closing;
    private bool _watchdog;
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
                var why = start(stream);
                if (why is not null) return why;
                _mod = mod;
                _stream = stream;
                _sent.Clear();
                _sentChars = 0;
                _macroEnds.Clear();
                _macrosSent.Clear();
                _watchdog = false;
                _overStartUtc = _clock.UtcNow;
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
        lock (_gate)
        {
            if (_disposed || _stream is not { } stream || _mod is not { } mod) return false;
            var now = _clock.UtcNow;
            if (now - _overStartUtc >= MaxOver + StallGrace)
            {
                _watchdog = true;
                stalled = true;
            }
            else
            {
                var ahead = (int)(AheadSeconds * _tx.OutputSampleRate);
                while (stream.Buffered < ahead && !_closing)
                {
                    if (now - _overStartUtc >= MaxOver || (_waiting.Length == 0 && !_endAfter && now - _lastActivityUtc >= MaxIdle))
                    {
                        _watchdog = true;
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
                        foreach (var m in _macroEnds.Where(m => m.End == _sentChars)) _macrosSent.Add(m.Key);
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
        if (stalled)
        {
            _ = _tx.HaltAsync();
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
        _stream!.Write(scaled);
    }

    private void OnStreamEnded(bool completed)
    {
        KeyboardOver over;
        lock (_gate)
        {
            if (_stream is null) return;
            over = new KeyboardOver(_sent.ToString(), [.. _macrosSent], completed && !_watchdog, _watchdog);
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
