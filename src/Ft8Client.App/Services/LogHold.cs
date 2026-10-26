// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Log;

namespace Ft8Client.App.Services;

/// <summary>
/// Holds a just-logged contact back from leaving the PC (QRZ and eQSL uploads, LoTW, the WSJT-X UDP "logged" message)
/// while the operator can still correct it from the Logged notice: <see cref="Window"/> by default, for as long as it is
/// kept (pointer on the notice, editor open), and no longer once released. It is in the local log from the start.
/// A kept contact's upload is pushed back two minutes at a time, so a crash mid-edit delays it only that long.
/// </summary>
public sealed class LogHold(UploadQueueRepository queue, QsoRepository qsos, Func<DateTime> utcNow)
{
    /// <summary>How long a contact is held when nobody touches its notice.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(6);

    private static readonly TimeSpan KeepStep = TimeSpan.FromMinutes(2);

    private readonly Lock _gate = new();
    private readonly Dictionary<long, Held> _held = [];

    /// <summary>Raised when a contact may leave the PC, with its row as it is now (after any edit).</summary>
    public event Action<QsoRecord>? Released;

    /// <summary>Contacts still held.</summary>
    public int Count
    {
        get
        {
            lock (_gate) return _held.Count;
        }
    }

    /// <summary>Starts holding a stored contact. Returns when its uploads should first be tried.</summary>
    public DateTime Add(long qsoId)
    {
        var until = utcNow() + Window;
        lock (_gate) _held[qsoId] = new Held { UntilUtc = until };
        return until;
    }

    /// <summary>Keeps a contact held (true) until <see cref="Keep"/>(false) starts its window again or it is released.</summary>
    public void Keep(long qsoId, bool keep)
    {
        var now = utcNow();
        DateTime next;
        lock (_gate)
        {
            if (!_held.TryGetValue(qsoId, out var h)) return;
            h.Kept = keep;
            h.UntilUtc = keep ? now + KeepStep : now + Window;
            next = h.UntilUtc;
        }
        queue.Postpone(qsoId, next);
    }

    /// <summary>Lets a contact go now: its uploads become due and <see cref="Released"/> is raised. Does nothing if not held.</summary>
    public void Release(long qsoId)
    {
        lock (_gate)
        {
            if (!_held.Remove(qsoId)) return;
        }
        Let(qsoId);
    }

    /// <summary>Releases contacts whose time is up and pushes back kept ones; called a few times a second.</summary>
    public void Tick()
    {
        var now = utcNow();
        List<long> due = [];
        List<(long Id, DateTime Until)> extend = [];
        lock (_gate)
        {
            foreach (var (id, h) in _held)
            {
                if (!h.Kept && now >= h.UntilUtc) due.Add(id);
                else if (h.Kept && h.UntilUtc - now < KeepStep / 2)
                {
                    h.UntilUtc = now + KeepStep;
                    extend.Add((id, h.UntilUtc));
                }
            }
            foreach (var id in due) _held.Remove(id);
        }
        foreach (var (id, until) in extend) queue.Postpone(id, until);
        foreach (var id in due) Let(id);
    }

    /// <summary>Releases everything, on exit.</summary>
    public void ReleaseAll()
    {
        List<long> all;
        lock (_gate)
        {
            all = [.. _held.Keys];
            _held.Clear();
        }
        foreach (var id in all) Let(id);
    }

    private void Let(long qsoId)
    {
        queue.Postpone(qsoId, utcNow());
        if (qsos.Get(qsoId) is { } q) Released?.Invoke(q);
    }

    private sealed class Held
    {
        public DateTime UntilUtc { get; set; }

        public bool Kept { get; set; }
    }
}
