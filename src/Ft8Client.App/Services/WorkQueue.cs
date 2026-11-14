// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.Concurrent;

namespace Ft8Client.App.Services;

/// <summary>
/// One background thread that runs posted work in the order it was posted. The session hands it everything the slot
/// thread must not wait for when a transmission starts: writing the contact to the log, the journal, and rebuilding
/// the screen. A database that is locked or slow then delays those, not PTT and the next slot's marks.
/// </summary>
public sealed class WorkQueue : IDisposable
{
    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private readonly Action<Exception> _onError;
    private int _pending;

    /// <summary>Starts the thread. <paramref name="onError"/> is told of work that threw; the queue carries on.</summary>
    public WorkQueue(string name, Action<Exception> onError)
    {
        _onError = onError;
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.Start();
    }

    /// <summary>Work posted and not yet finished.</summary>
    public int Pending => Volatile.Read(ref _pending);

    /// <summary>Queues <paramref name="work"/>. Never blocks. After <see cref="Dispose"/> the work runs on the caller.</summary>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        Interlocked.Increment(ref _pending);
        try
        {
            _work.Add(work);
            return;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // Closing: nothing is dropped, a contact still has to reach the log.
        }
        RunOne(work);
    }

    /// <summary>Waits until everything posted so far has run, up to <paramref name="timeout"/>. True if it has.</summary>
    public bool Flush(TimeSpan timeout)
    {
        var until = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Pending > 0)
        {
            if (Environment.TickCount64 >= until) return false;
            Thread.Sleep(1);
        }
        return true;
    }

    private void Run()
    {
        foreach (var work in _work.GetConsumingEnumerable()) RunOne(work);
    }

    private void RunOne(Action work)
    {
        try
        {
            work();
        }
        catch (Exception ex)
        {
            try
            {
                _onError(ex);
            }
            catch (Exception)
            {
                // The error handler must not end the thread either.
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
        }
    }

    /// <summary>Runs what is still queued (a contact waiting to be logged), then stops the thread.</summary>
    public void Dispose()
    {
        if (_work.IsAddingCompleted) return;
        _work.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
    }
}
