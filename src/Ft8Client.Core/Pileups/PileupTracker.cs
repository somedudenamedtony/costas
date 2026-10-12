// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Pileups;

/// <summary>
/// Watches who calls whom, so the operator can see the pileup on a station before calling it (docs/04-domain-logic.md,
/// section 12). A caller is a station heard sending its grid or a bare call to another station. A pick is a station
/// sending a message to a caller it was not already working. Not thread-safe; the session calls it from one worker.
/// </summary>
public sealed class PileupTracker
{
    /// <summary>How long callers and picks are kept.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>Most picks used to describe how a station chooses.</summary>
    public const int MaxPicks = 5;

    private readonly Dictionary<string, List<PileupCaller>> _callers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PileupPick>> _picks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _partner = new(StringComparer.Ordinal);
    private readonly Mode _mode;

    /// <summary>Creates a tracker for one mode.</summary>
    public PileupTracker(Mode mode) => _mode = mode;

    /// <summary>Forgets everything (band or mode change).</summary>
    public void Clear()
    {
        _callers.Clear();
        _picks.Clear();
        _partner.Clear();
    }

    /// <summary>Applies the decodes of one received slot. Low-confidence decodes and unresolved calls are ignored.</summary>
    public void ApplySlot(DateTime slotStartUtc, IEnumerable<HeardMessage> heard)
    {
        var slotLength = ModeInfo.SlotLength(_mode);
        var messages = heard.Where(h => !h.Decode.LowConfidence)
            .SelectMany(h => h.Message.Expand().Select(m => (h.Decode, Message: m)))
            .ToList();

        // Picks first: they are judged against the callers of the slot before, not this one.
        foreach (var (_, m) in messages)
        {
            if (m.SenderCall is not { } sender || !Callsign.IsValid(sender)) continue;
            var from = Callsign.Normalize(sender);
            if (m.Kind == MessageKind.Cq)
            {
                _partner.Remove(from);
                continue;
            }
            if (m.To is not { Unresolved: false } to || Callsign.EqualsCall(to.Call, from)) continue;
            var picked = Callsign.Normalize(to.Call);
            var isNew = !_partner.TryGetValue(from, out var current) || current != picked;
            _partner[from] = picked;
            // Answering a caller starts with a report; a grid or a bare call is the station calling someone itself.
            if (!isNew || m.Kind != MessageKind.Report || !_callers.TryGetValue(from, out var list)) continue;
            var before = list.Where(c => c.SlotUtc == slotStartUtc - slotLength).ToList();
            if (before.Count == 0) continue;
            var heardPicked = before.FirstOrDefault(c => c.Call == picked);
            var strongest = heardPicked is not null && before.All(c => c.Snr <= heardPicked.Snr);
            Add(_picks, from, new PileupPick(picked, slotStartUtc, before.Count, heardPicked is not null, strongest));
        }

        foreach (var (d, m) in messages)
        {
            if (m.Kind is not (MessageKind.GridReply or MessageKind.Bare)) continue;
            var from = m.SenderCall;
            if (from is null || !Callsign.IsValid(from)) continue;
            if (m.To is not { Unresolved: false } to || Callsign.EqualsCall(to.Call, from)) continue;
            var target = Callsign.Normalize(to.Call);
            var caller = Callsign.Normalize(from);
            if (!_callers.TryGetValue(target, out var list)) _callers[target] = list = [];
            var same = list.FindIndex(c => c.Call == caller && c.SlotUtc == slotStartUtc);
            if (same >= 0)
            {
                if (list[same].Snr < d.Snr) list[same] = list[same] with { Snr = d.Snr, OffsetHz = d.OffsetHz };
                continue;
            }
            list.Add(new PileupCaller(caller, slotStartUtc, d.Snr, d.OffsetHz));
        }

        Prune(slotStartUtc);
    }

    /// <summary>The pileup on <paramref name="target"/>, or null when nobody has been heard calling it and it has made no picks.</summary>
    /// <param name="target">The station.</param>
    /// <param name="nowUtc">Now; callers older than two slots of the target's answers are not current.</param>
    public Pileup? For(string target, DateTime nowUtc)
    {
        var t = Callsign.Normalize(target);
        var callers = _callers.GetValueOrDefault(t) ?? [];
        var picks = _picks.GetValueOrDefault(t) ?? [];
        if (callers.Count == 0 && picks.Count == 0) return null;

        // Callers count as current until the station has had two chances to answer them.
        var current = callers.Count == 0 ? (DateTime?)null : callers.Max(c => c.SlotUtc);
        if (current is { } c0 && nowUtc - c0 > 4 * ModeInfo.SlotLength(_mode)) current = null;
        return new Pileup
        {
            Target = t,
            CallersSlotUtc = current,
            Callers = current is { } slot ? callers.Where(c => c.SlotUtc == slot).OrderByDescending(c => c.Snr).ThenBy(c => c.Call, StringComparer.Ordinal).ToList() : [],
            Picks = picks.OrderByDescending(p => p.SlotUtc).ToList(),
        };
    }

    /// <summary>The pileups on every station with current callers or recent picks.</summary>
    public IReadOnlyDictionary<string, Pileup> All(DateTime nowUtc)
    {
        var result = new Dictionary<string, Pileup>(StringComparer.Ordinal);
        foreach (var t in _callers.Keys.Concat(_picks.Keys).Distinct())
        {
            if (For(t, nowUtc) is { } p && (p.Callers.Count > 0 || p.Picks.Count > 0)) result[t] = p;
        }
        return result;
    }

    private void Prune(DateTime nowUtc)
    {
        foreach (var key in _callers.Keys.ToList())
        {
            _callers[key].RemoveAll(c => nowUtc - c.SlotUtc > Window);
            if (_callers[key].Count == 0) _callers.Remove(key);
        }
        foreach (var key in _picks.Keys.ToList())
        {
            _picks[key].RemoveAll(p => nowUtc - p.SlotUtc > Window);
            if (_picks[key].Count == 0) _picks.Remove(key);
        }
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T item)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(item);
    }
}
