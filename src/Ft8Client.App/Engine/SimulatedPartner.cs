// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Encoding;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Engine;

/// <summary>
/// The scripted partner for <c>--simulate-partner</c>: when the app transmits to a station, that station's next message
/// is synthesised with the encoder and mixed into the following slot. When the app calls CQ, the next call from the
/// partner list answers. Partner calls come from a samples file, never from production code.
/// </summary>
public sealed class SimulatedPartner
{
    private readonly object _gate = new();
    private readonly Queue<(string Call, string Grid)> _callers;
    private readonly Dictionary<DateTime, List<(string Text, int Offset, int Snr)>> _pending = [];
    private readonly Random _rng = new(7);

    /// <summary>Creates a partner with stations that answer CQ, in order.</summary>
    public SimulatedPartner(IEnumerable<(string Call, string Grid)> callers)
    {
        _callers = new Queue<(string, string)>(callers);
    }

    /// <summary>Loads callers from a file of "CALL GRID" lines (samples/partners.txt).</summary>
    public static SimulatedPartner FromFile(string path) =>
        new(File.ReadAllLines(path)
            .Select(l => l.Split('#')[0].Trim())
            .Where(l => l.Length > 0)
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p.Length >= 2)
            .Select(p => (p[0].ToUpperInvariant(), p[1].ToUpperInvariant())));

    /// <summary>Messages sent by the partner so far.</summary>
    public List<string> Sent { get; } = [];

    /// <summary>Hook for <see cref="Session.Transmitting"/>: decides the partner's reply to what we sent.</summary>
    public void OnTransmitted(PreparedTx tx, DateTime startedUtc)
    {
        var m = MessageParser.Parse(tx.Plan.Message);
        var me = m.SenderCall;
        if (me is null) return;
        string? reply = null;
        var to = m.To?.Call;
        switch (m.Kind)
        {
            case MessageKind.Cq:
                lock (_gate)
                {
                    if (_callers.Count > 0)
                    {
                        var (call, grid) = _callers.Dequeue();
                        reply = $"{me} {call} {grid}";
                    }
                }
                break;
            case MessageKind.GridReply:
                reply = $"{me} {to} {Report.Format(-10 - _rng.Next(8))}";
                break;
            case MessageKind.Report:
                reply = $"{me} {to} R{Report.Format(-8 - _rng.Next(8))}";
                break;
            case MessageKind.RogerReport:
                reply = $"{me} {to} RR73";
                break;
            case MessageKind.RogerBye:
            case MessageKind.Roger:
                reply = $"{me} {to} 73";
                break;
        }
        if (reply is null) return;
        var next = tx.Plan.SlotStartUtc + ModeInfo.SlotLength(tx.Mode);
        lock (_gate)
        {
            if (!_pending.TryGetValue(next, out var list)) _pending[next] = list = [];
            list.Add((reply, 600 + _rng.Next(2000), -12));
            Sent.Add(reply);
        }
    }

    /// <summary>Mixer for <see cref="SimulatedSlotSource"/>: the partner's signals for a slot at 12 kHz, or null.</summary>
    public float[]? Mix(DateTime slotStartUtc, Mode mode)
    {
        List<(string Text, int Offset, int Snr)>? list;
        lock (_gate)
        {
            if (!_pending.Remove(slotStartUtc, out list)) return null;
        }
        var buf = new float[ModeInfo.DecoderSamples(mode)];
        var start = ModeInfo.TxStartMilliseconds(mode) * ModeInfo.DecoderSampleRate / 1000;
        foreach (var (text, offset, _) in list)
        {
            var enc = FtxEncoder.Encode(text, mode);
            if (!enc.Ok) continue;
            var w = GfskSynth.Render(enc.Tones, mode, offset, ModeInfo.DecoderSampleRate, 0.05);
            for (var i = 0; i < w.Length && start + i < buf.Length; i++) buf[start + i] += w[i];
        }
        return buf;
    }
}
