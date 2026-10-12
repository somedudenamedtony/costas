// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Remote;

namespace Ft8Client.App.Tests;

/// <summary>Records what the remote page asked the session to do.</summary>
internal sealed class FakeRemoteTarget : IRemoteTarget
{
    public SessionSnapshot Snapshot { get; set; } = new();

    public bool WantsTransmit { get; set; }

    public List<string> Calls { get; } = [];

    public List<string?> Halts { get; } = [];

    public int? WatchdogCap { get; private set; }

    public int ActivityCount { get; private set; }

    public void Activity() => ActivityCount++;

    public bool CallStation(string call)
    {
        Calls.Add("call " + call);
        return call != "NOBODY";
    }

    public void AnswerCaller(string call) => Calls.Add("answer " + call);

    public void AnswerAfterThisContact(string call) => Calls.Add("answerAfter " + call);

    public void ToggleCq() => Calls.Add("cq");

    public void Resend() => Calls.Add("resend");

    public void LogNow() => Calls.Add("logNow");

    public void JumpTo(int step) => Calls.Add("jump " + step);

    public Task AbandonAsync()
    {
        Calls.Add("abandon");
        return Task.CompletedTask;
    }

    public Task HaltAsync(string? reason)
    {
        Halts.Add(reason);
        WantsTransmit = false;
        return Task.CompletedTask;
    }

    public void SetWatchdogCap(int? minutes) => WatchdogCap = minutes;
}
