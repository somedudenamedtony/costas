// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Time;

namespace Ft8Client.Audio.Timing;

/// <summary>
/// Derives slot boundaries from system UTC, advanced by a monotonic timer, so that a system time change mid-slot
/// never causes a double or missed slot. The wall clock is re-read only at slot boundaries, and a slot start is never
/// announced twice. Driven by <see cref="Poll"/> (a dedicated thread in the app, by hand in tests).
/// </summary>
public sealed class SlotClock
{
    private static readonly TimeSpan ResyncThreshold = TimeSpan.FromMilliseconds(50);
    private readonly IClock _clock;
    private readonly object _gate = new();
    private DateTime _anchorUtc;
    private long _anchorTicks;
    private DateTime _current;
    private DateTime _lastAnnounced = DateTime.MinValue;
    private int _nextMark;
    private bool _started;
    private Mode _mode;
    private Mode? _pendingMode;
    private Thread? _thread;
    private volatile bool _run;

    /// <summary>Creates a clock.</summary>
    public SlotClock(IClock clock, Mode mode, int pttLeadMs = 200)
    {
        _clock = clock;
        _mode = mode;
        PttLeadMs = pttLeadMs;
    }

    /// <summary>Raised on the polling thread for each slot mark. Handlers must not block.</summary>
    public event Action<SlotEvent>? Event;

    /// <summary>Mode of the current slot.</summary>
    public Mode Mode
    {
        get { lock (_gate) return _mode; }
    }

    /// <summary>PTT lead before Tx audio, in ms.</summary>
    public int PttLeadMs { get; set; }

    /// <summary>The estimated UTC time (wall clock anchored at the last boundary plus monotonic time since).</summary>
    public DateTime NowUtc
    {
        get
        {
            lock (_gate)
            {
                EnsureStarted();
                return Estimate();
            }
        }
    }

    /// <summary>Start of the current slot.</summary>
    public DateTime CurrentSlotUtc
    {
        get
        {
            lock (_gate)
            {
                EnsureStarted();
                return _current;
            }
        }
    }

    /// <summary>Changes mode at the next slot boundary.</summary>
    public void SetMode(Mode mode)
    {
        lock (_gate) _pendingMode = mode;
    }

    /// <summary>Offset of a mark from the slot start.</summary>
    public TimeSpan OffsetOf(SlotMark mark, Mode mode) => mark switch
    {
        SlotMark.Start => TimeSpan.Zero,
        SlotMark.PttOn => TimeSpan.FromMilliseconds(ModeInfo.TxStartMilliseconds(mode) - PttLeadMs),
        SlotMark.TxStart => TimeSpan.FromMilliseconds(ModeInfo.TxStartMilliseconds(mode)),
        _ => mode == Mode.Ft8 ? TimeSpan.FromSeconds(13.6) : TimeSpan.FromSeconds(6.1),
    };

    /// <summary>Checks the time and raises any marks that are due, in order.</summary>
    public void Poll()
    {
        var events = new List<SlotEvent>(2);
        lock (_gate)
        {
            if (!_started)
            {
                EnsureStarted();
                return;
            }
            var now = Estimate();
            while (true)
            {
                if (_nextMark < 4)
                {
                    var mark = (SlotMark)_nextMark;
                    var due = _current + OffsetOf(mark, _mode);
                    if (now < due) break;
                    if (mark != SlotMark.Start || _current > _lastAnnounced)
                    {
                        events.Add(new SlotEvent(mark, _current, _mode, now - due));
                        if (mark == SlotMark.Start) _lastAnnounced = _current;
                    }
                    _nextMark++;
                    continue;
                }

                var next = _current + ModeInfo.SlotLength(_mode);
                if (now < next) break;

                // Boundary: re-read the wall clock and re-anchor if it has moved.
                var wall = _clock.UtcNow;
                var ticks = _clock.Ticks;
                if (_pendingMode is { } m)
                {
                    _mode = m;
                    _pendingMode = null;
                }
                var est = now;
                if ((wall - est).Duration() > ResyncThreshold)
                {
                    _anchorUtc = wall;
                    _anchorTicks = ticks;
                    now = wall;
                    var wallSlot = SlotMath.SlotStart(wall, _mode);
                    if (wallSlot <= _lastAnnounced)
                    {
                        // Clock went back: wait for the slot after the last one announced.
                        _current = _lastAnnounced;
                        _nextMark = 4;
                        continue;
                    }
                    _current = wallSlot;
                    _nextMark = 0;
                    continue;
                }
                _current = SlotMath.SlotStart(next, _mode);
                _nextMark = 0;
            }
        }
        foreach (var e in events) Event?.Invoke(e);
    }

    /// <summary>Starts a dedicated high-priority thread that polls every millisecond.</summary>
    public void Start()
    {
        if (_thread is not null) return;
        _run = true;
        _thread = new Thread(() =>
        {
            while (_run)
            {
                Poll();
                Thread.Sleep(1);
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Highest, Name = "SlotClock" };
        _thread.Start();
    }

    /// <summary>Stops the polling thread.</summary>
    public void Stop()
    {
        _run = false;
        _thread?.Join(500);
        _thread = null;
    }

    private void EnsureStarted()
    {
        if (_started) return;
        _anchorUtc = _clock.UtcNow;
        _anchorTicks = _clock.Ticks;
        _current = SlotMath.SlotStart(_anchorUtc, _mode);
        // Joined mid-slot: skip this partial slot.
        _lastAnnounced = _current;
        _nextMark = 4;
        _started = true;
    }

    private DateTime Estimate() => _anchorUtc + TimeSpan.FromTicks(_clock.Ticks - _anchorTicks);
}
