// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Diagnostics;

namespace Ft8Client.UpdateSplash;

// Started by Costas just before it closes for an update (UpdateSplashLauncher); see SplashProtocol for the hand-over.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 1;
        var options = SplashOptions.Parse(args);
        using var started = new EventWaitHandle(false, EventResetMode.ManualReset, SplashProtocol.StartedEvent);
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, SplashProtocol.ReadyEvent);
        // Left set by an earlier update in this session if its splash is somehow still around.
        started.Reset();
        ready.Reset();
        using var old = Find(options.ProcessId);
        return SplashWindow.Run(options, () => (Exited(old), started.WaitOne(0), ready.WaitOne(0)), SignalShown);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void SignalShown()
    {
        if (EventWaitHandle.TryOpenExisting(SplashProtocol.ShownEvent, out var shown))
        {
            using (shown) shown.Set();
        }
    }

    private static Process? Find(int pid)
    {
        if (pid <= 0) return null;
        try
        {
            return Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return null; // Already gone.
        }
    }

    private static bool Exited(Process? p)
    {
        if (p is null) return true;
        try
        {
            return p.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }
}
