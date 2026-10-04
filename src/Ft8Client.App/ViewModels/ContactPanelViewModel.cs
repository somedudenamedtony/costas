// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
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

    /// <summary>Report sent, blank until sent.</summary>
    public string Sent { get; private init; } = string.Empty;

    /// <summary>Report received, blank until received.</summary>
    public string Received { get; private init; } = string.Empty;

    /// <summary>Log now is enabled.</summary>
    public bool CanLogNow { get; private init; }

    /// <summary>The contact is still running.</summary>
    public bool Active { get; private init; }

    /// <summary>The strip.</summary>
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
        var steps = new List<StepViewModel>();
        for (var i = 0; i < c.Steps.Count; i++)
        {
            var st = c.Steps[i];
            var who = st.Mine ? "You" : c.DxCall;
            var count = st.Count > 1 ? $" ×{st.Count}" : string.Empty;
            StepLook look;
            string header;
            switch (st.State)
            {
                case StepState.Done:
                    look = StepLook.Done;
                    header = $"{who} · {(st.Mine ? "sent" : "received")} {Time(st.TimeUtc)}{count}";
                    break;
                case StepState.Now when st.Mine && s.Transmitting && s.TransmittingMessage == st.Message:
                    look = StepLook.Transmitting;
                    var left = currentSlot + TimeSpan.FromSeconds(0.5 + 12.64) - nowUtc;
                    header = $"You · sending, {Math.Max(0, (int)Math.Ceiling(left.TotalSeconds))} s left{count}";
                    break;
                case StepState.Now when st.Mine:
                    look = StepLook.Waiting;
                    header = $"You · sending next slot{count}";
                    break;
                case StepState.Now:
                    look = StepLook.Waiting;
                    header = $"{who} · expected {Time(currentSlot + slotLen)}{count}";
                    break;
                default:
                    look = StepLook.Upcoming;
                    header = st.Mine ? (i == c.Steps.Count - 1 || (i == c.Steps.Count - 2 && !c.Steps[^1].Mine) ? "You · then logged" : "You · next") : $"{who} · expected";
                    break;
            }
            steps.Add(new StepViewModel(i, st.Mine, header, st.Message, look, st.Mine && st.State == StepState.Upcoming && c.Outcome == ContactOutcome.InProgress));
        }
        return new ContactPanelViewModel
        {
            Caption = c.Caption,
            Call = c.DxCall,
            Need = need is { } n ? StationText.Need(n.Tag) : string.Empty,
            NeedKind = need is { } n2 ? StationRowViewModel.KindOf(n2.Tag) : TextKind.Normal,
            Subline = string.Join(" · ", parts),
            Sent = c.ReportSent is { } rs ? Report.Format(rs) : string.Empty,
            Received = c.ReportReceived is { } rr ? Report.Format(rr) : string.Empty,
            CanLogNow = c.CanLogNow,
            Active = c.Outcome == ContactOutcome.InProgress,
            Steps = steps,
        };
    }

    private static string Time(DateTime? t) => t?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
}
