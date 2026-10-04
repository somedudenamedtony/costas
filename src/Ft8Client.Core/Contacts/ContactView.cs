// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>What the top slot shows for a contact.</summary>
/// <param name="DxCall">DX call.</param>
/// <param name="DxGrid">DX grid, if known.</param>
/// <param name="Flow">How it started.</param>
/// <param name="Outcome">In progress or how it ended.</param>
/// <param name="ReportSent">Report sent, once sent.</param>
/// <param name="ReportReceived">Report received, once received.</param>
/// <param name="Steps">The conversation strip.</param>
/// <param name="Logged">True once logged.</param>
/// <param name="CanLogNow">True when both reports are exchanged and it is not logged yet.</param>
/// <param name="StatusText">One line for the bottom bar.</param>
public sealed record ContactView(string DxCall, string? DxGrid, ContactFlow Flow, ContactOutcome Outcome, int? ReportSent, int? ReportReceived,
                                 IReadOnlyList<ContactStep> Steps, bool Logged, bool CanLogNow, string StatusText)
{
    /// <summary>"Contact in progress", "Calling", "Logged", "No reply", "Stopped by watchdog", "Abandoned".</summary>
    public string Caption => Outcome switch
    {
        ContactOutcome.Logged => Logged ? "Logged" : "Complete, not logged",
        ContactOutcome.NoReply => "No reply",
        ContactOutcome.Watchdog => "Stopped by watchdog",
        ContactOutcome.Abandoned => "Abandoned",
        _ => Logged ? "Logged" : ReportReceived is null && Steps.Count(s => !s.Mine && s.State == StepState.Done) == 0 ? "Calling" : "Contact in progress",
    };
}
