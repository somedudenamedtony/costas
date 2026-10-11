// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;

namespace Ft8Client.App.Services;

/// <summary>
/// Which calls still need a QRZ lookup, in order. A call is asked about once per run: once it is settled (found, not
/// found, or read from the cache) it is never queued again, and a failed lookup waits before it may be queued again.
/// Thread-safe.
/// </summary>
public sealed class LookupQueue(int capacity = 40)
{
    private readonly object _gate = new();
    private readonly HashSet<string> _settled = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _pending = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _retryAfter = new(StringComparer.Ordinal);

    /// <summary>Number of calls waiting.</summary>
    public int Count
    {
        get
        {
            lock (_gate) return _pending.Count;
        }
    }

    /// <summary>
    /// Asks for a call to be looked up. Urgent calls (the selected station, the one in a contact) go to the front.
    /// Returns true when the call was added or moved.
    /// </summary>
    public bool Request(string call, bool urgent, DateTime nowUtc)
    {
        var c = Callsign.Normalize(call);
        lock (_gate)
        {
            if (_settled.Contains(c)) return false;
            if (_retryAfter.TryGetValue(c, out var after) && nowUtc < after) return false;
            if (_nodes.TryGetValue(c, out var node))
            {
                if (!urgent || _pending.First == node) return false;
                _pending.Remove(node);
                _pending.AddFirst(node);
                return true;
            }
            if (!urgent && _pending.Count >= capacity) return false;
            _nodes[c] = urgent ? _pending.AddFirst(c) : _pending.AddLast(c);
            while (_pending.Count > capacity) Drop(_pending.Last!);
            return true;
        }
    }

    /// <summary>Takes the next call to look up, or null.</summary>
    public string? Next()
    {
        lock (_gate)
        {
            if (_pending.First is not { } first) return null;
            Drop(first);
            return first.Value;
        }
    }

    /// <summary>The call has its answer (found or not) for the rest of this run.</summary>
    public void Settle(string call)
    {
        var c = Callsign.Normalize(call);
        lock (_gate)
        {
            _settled.Add(c);
            _retryAfter.Remove(c);
            if (_nodes.TryGetValue(c, out var node)) Drop(node);
        }
    }

    /// <summary>The lookup failed (QRZ unreachable, refused); the call may be asked for again after <paramref name="retryAfterUtc"/>.</summary>
    public void Failed(string call, DateTime retryAfterUtc)
    {
        lock (_gate) _retryAfter[Callsign.Normalize(call)] = retryAfterUtc;
    }

    /// <summary>True when the call already has its answer this run.</summary>
    public bool IsSettled(string call)
    {
        lock (_gate) return _settled.Contains(Callsign.Normalize(call));
    }

    /// <summary>Forgets waiting calls and failures (new credentials); settled answers are kept.</summary>
    public void ClearPending()
    {
        lock (_gate)
        {
            _pending.Clear();
            _nodes.Clear();
            _retryAfter.Clear();
        }
    }

    private void Drop(LinkedListNode<string> node)
    {
        _pending.Remove(node);
        _nodes.Remove(node.Value);
    }
}
