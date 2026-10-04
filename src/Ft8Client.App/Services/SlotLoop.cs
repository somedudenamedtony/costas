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

    /// <summary>Starts the thread.</summary>
    public void Start()
    {
        _run = true;
        _thread = new Thread(() =>
        {
            while (_run)
            {
                clock.Poll();
                tx.Poll();
                Thread.Sleep(1);
            }
        })
        { IsBackground = true, Priority = ThreadPriority.Highest, Name = "SlotLoop" };
        _thread.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
    }
}
