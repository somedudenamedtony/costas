// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Audio.Timing;

namespace Ft8Client.App.Services;

/// <summary>The dedicated slot thread: polls the slot clock and the transmitter every millisecond. Never waits on I/O.</summary>
public sealed class SlotLoop(SlotClock clock, Transmitter tx) : IDisposable
{
    private Thread? _thread;
    private volatile bool _run;
    private long _lastErrorTicks = long.MinValue;

    /// <summary>Starts the thread.</summary>
    public void Start()
    {
        _run = true;
        _thread = new Thread(() =>
        {
            while (_run)
            {
                Guarded(clock.Poll);
                Guarded(tx.Poll);
                Thread.Sleep(1);
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Highest, Name = "SlotLoop" };
        _thread.Start();
    }

    // An exception on this thread would end the process, possibly with PTT keyed over CAT. Stop transmitting instead,
    // say why in the log (at most every 10 s, as a fault can repeat every millisecond), and keep the slots running.
    private void Guarded(Action poll)
    {
        try
        {
            poll();
        }
        catch (Exception ex)
        {
            var now = Environment.TickCount64;
            if (now - Interlocked.Read(ref _lastErrorTicks) < 10_000) return;
            Interlocked.Exchange(ref _lastErrorTicks, now);
            Serilog.Log.Error(ex, "Slot thread error; transmit halted");
            _ = tx.HaltAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
    }
}
