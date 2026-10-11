// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;

namespace Ft8Client.App.ViewModels;

/// <summary>The top slot while in a contact.</summary>
public sealed class ContactPanelViewModel
{
    /// <summary>Shown in place of a report not exchanged yet.</summary>
    public const string NotYet = "--";

    private ContactPanelViewModel()
    {
    }

    /// <summary>"Contact in progress", "Calling", "Logged", "No reply".</summary>
    public string Caption { get; private init; } = string.Empty;

    /// <summary>DX call.</summary>
    public string Call { get; private init; } = string.Empty;

    /// <summary>Need tag beside the call.</summary>
    public string Need { get; private init; } = string.Empty;

    /// <summary>Need colour.</summary>
    public TextKind NeedKind { get; private init; }

    /// <summary>Name · place · distance.</summary>
    public string Subline { get; private init; } = string.Empty;

    /// <summary>Report sent in dB, "--" until sent.</summary>
    public string Sent { get; private init; } = string.Empty;

    /// <summary>Report received in dB, "--" until received.</summary>
    public string Received { get; private init; } = string.Empty;

    /// <summary>Log now is enabled.</summary>
    public bool CanLogNow { get; private init; }

    /// <summary>The contact is still running.</summary>
    public bool Active { get; private init; }

    /// <summary>The strip, one box per step in order.</summary>
    public IReadOnlyList<StepViewModel> Steps { get; private init; } = [];

    /// <summary>Builds the panel from a snapshot.</summary>
    public static ContactPanelViewModel From(ContactView c, SessionSnapshot s, DateTime nowUtc, DistanceUnit units, string? qrzName)
    {
        var station = s.Stations.FirstOrDefault(x => Callsign.EqualsCall(x.Call, c.DxCall));
        var need = s.Rank.Line.Concat(s.Rank.Watching).Concat(s.Rank.Worked).FirstOrDefault(x => Callsign.EqualsCall(x.Station.Call, c.DxCall))?.Need;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(qrzName)) parts.Add(qrzName);
        if (station is not null) parts.Add(StationText.Where(station, units));
        var slotLen = ModeInfo.SlotLength(s.Mode);
        var currentSlot = SlotMath.SlotStart(nowUtc, s.Mode);
        var txIndex = TransmittingIndex(c, s, currentSlot);
        var steps = new List<StepViewModel>();
        for (var i = 0; i < c.Steps.Count; i++)
        {
            var st = c.Steps[i];
            var who = st.Mine ? "You" : c.DxCall;
            var count = st.Count > 1 ? $" ×{st.Count}" : string.Empty;
            StepLook look;
            string time;
            string status;
            if (i == txIndex)
            {
                // The engine counts a message as sent when it starts, so a step on the air now is marked done; show it as sending.
                look = StepLook.Transmitting;
                time = Time(st.TimeUtc ?? currentSlot);
                var left = currentSlot + ModeInfo.TxEnd(s.Mode) - nowUtc;
                status = $"Sending{count} · {Math.Max(0, (int)Math.Ceiling(left.TotalSeconds))} s left";
            }
            else
            {
                switch (st.State)
                {
                    case StepState.Done:
                        look = StepLook.Done;
                        time = Time(st.TimeUtc);
                        status = (st.Mine ? "Sent" : "Received") + count;
                        break;
                    case StepState.Now when st.Mine:
                        look = StepLook.Waiting;
                        time = string.Empty;
                        status = $"Sending next slot{count}";
                        break;
                    case StepState.Now:
                        // The reply is due in the slot after my last transmission. While that transmission is still on the air the
                        // reply is not being waited for yet, so it is not highlighted.
                        look = txIndex >= 0 && txIndex < i ? StepLook.Upcoming : StepLook.Waiting;
                        time = Time(ExpectedReply(c, i, s.Mode, currentSlot, slotLen));
                        status = (look == StepLook.Waiting ? "Waiting for reply" : "Expected") + count;
                        break;
                    default:
                        look = StepLook.Upcoming;
                        time = string.Empty;
                        status = st.Mine ? (i == c.Steps.Count - 1 || (i == c.Steps.Count - 2 && !c.Steps[^1].Mine) ? "Then logged" : "Next") : "Expected";
                        break;
                }
            }
            steps.Add(new StepViewModel(i, st.Mine, who, time, st.Message, status, look,
                st.Mine && st.State == StepState.Upcoming && c.Outcome == ContactOutcome.InProgress));
        }
        return new ContactPanelViewModel
        {
            Caption = c.Caption,
            Call = c.DxCall,
            Need = need is { } n ? StationText.Need(n.Tag) : string.Empty,
            NeedKind = need is { } n2 ? StationRowViewModel.KindOf(n2.Tag) : TextKind.Normal,
            Subline = string.Join(" · ", parts),
            Sent = c.ReportSent is { } rs ? Report.Format(rs) : NotYet,
            Received = c.ReportReceived is { } rr ? Report.Format(rr) : NotYet,
            CanLogNow = c.CanLogNow,
            Active = c.Outcome == ContactOutcome.InProgress,
            Steps = [.. steps.Select((x, i) => x with
            {
                First = i == 0,
                Last = i == steps.Count - 1,
                NextReached = i + 1 < steps.Count && steps[i + 1].Reached,
            })],
        };
    }

    // The mine step on the air now: the one still to send, or the one the engine already counted as sent in this slot.
    private static int TransmittingIndex(ContactView c, SessionSnapshot s, DateTime currentSlot)
    {
        if (!s.Transmitting || s.TransmittingMessage is null) return -1;
        for (var i = c.Steps.Count - 1; i >= 0; i--)
        {
            var st = c.Steps[i];
            if (!st.Mine || !string.Equals(st.Message, s.TransmittingMessage, StringComparison.Ordinal)) continue;
            if (st.State == StepState.Now || (st.State == StepState.Done && st.TimeUtc >= currentSlot)) return i;
        }
        return -1;
    }

    // The slot after my last transmission before this step, or the next slot when I have not sent yet.
    private static DateTime ExpectedReply(ContactView c, int index, Mode mode, DateTime currentSlot, TimeSpan slotLen)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (c.Steps[i] is { Mine: true, TimeUtc: { } sent }) return SlotMath.SlotStart(sent, mode) + slotLen;
        }
        return currentSlot + slotLen;
    }

    private static string Time(DateTime? t) => t?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
}
