// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;

namespace Ft8Client.App.Remote;

/// <summary>What the remote page can see and do: the session's snapshot and the same commands the Operate view sends.</summary>
public interface IRemoteTarget
{
    /// <summary>The latest snapshot.</summary>
    SessionSnapshot Snapshot { get; }

    /// <summary>True when something is (or is about to be) transmitted.</summary>
    bool WantsTransmit { get; }

    /// <summary>Operator input, for the Tx watchdog.</summary>
    void Activity();

    /// <summary>Calls a station; false when it is not in the station list.</summary>
    bool CallStation(string call);

    /// <summary>Answers a waiting caller now.</summary>
    void AnswerCaller(string call);

    /// <summary>Answers a waiting caller after this contact.</summary>
    void AnswerAfterThisContact(string call);

    /// <summary>Starts or stops calling CQ.</summary>
    void ToggleCq();

    /// <summary>Resends the current step.</summary>
    void Resend();

    /// <summary>PSK and RTTY: sends the macro on a function key. Returns why it was refused, or null.</summary>
    string? SendMacro(int key);

    /// <summary>Logs the contact now.</summary>
    void LogNow();

    /// <summary>Jumps to one of my upcoming steps.</summary>
    void JumpTo(int step);

    /// <summary>Abandons the contact.</summary>
    Task AbandonAsync();

    /// <summary>Halts transmit, with a reason for the bottom bar when the operator did not ask for it.</summary>
    Task HaltAsync(string? reason);

    /// <summary>Caps the Tx watchdog, or removes the cap with null.</summary>
    void SetWatchdogCap(int? minutes);
}
