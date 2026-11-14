// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Ft8Client.UpdateSplash;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Starts the update splash (CostasUpdateSplash.exe, published beside Costas.exe on Windows) just before Costas closes
/// for an update, and tells a running splash when Costas has started again. Without it the app vanished for about five
/// seconds while it closed, Velopack swapped its files and the new version started. See <see cref="SplashProtocol"/>.
/// </summary>
/// <remarks>
/// The splash is copied out of the install folder before it starts: Velopack renames that folder to apply the update,
/// which a program running from it would block. Everything here is best effort: with no splash (a build from source,
/// Linux, a copy that fails) the update goes ahead exactly as before.
/// </remarks>
public static partial class UpdateSplashLauncher
{
    /// <summary>How long Costas waits for the splash to be on screen before it closes anyway.</summary>
    public static readonly TimeSpan ShownWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Copies the splash from <paramref name="appFolder"/> to <paramref name="copyFolder"/>, starts it and waits (up to
    /// <paramref name="wait"/>) until its window shows. Returns the splash, or null when there is none.
    /// </summary>
    public static async Task<Process?> ShowAsync(string appFolder, string copyFolder, SplashOptions options, TimeSpan wait)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var source = Path.Combine(appFolder, SplashProtocol.ExeName);
        if (!File.Exists(source))
        {
            Log.Information("No update splash beside the app ({Path})", source);
            return null;
        }
        try
        {
            Directory.CreateDirectory(copyFolder);
            // A new name each time: a splash from an earlier attempt may still hold its copy. UpdateService.Start removes them.
            var copy = Path.Combine(copyFolder, $"splash-{Guid.NewGuid():N}.exe");
            File.Copy(source, copy);
            using var shown = new EventWaitHandle(false, EventResetMode.ManualReset, SplashProtocol.ShownEvent);
            shown.Reset();
            var start = new ProcessStartInfo(copy) { UseShellExecute = false, WorkingDirectory = copyFolder };
            foreach (var a in options.ToArguments()) start.ArgumentList.Add(a);
            var splash = Process.Start(start);
            if (splash is null) return null;
            // Costas is the foreground app, so it can let the splash come to the front as its own window closes.
            AllowSetForegroundWindow(splash.Id);
            var onScreen = await Task.Run(() => shown.WaitOne(wait)).ConfigureAwait(false);
            if (!onScreen) Log.Information("The update splash did not show within {Wait}; closing anyway", wait);
            return splash;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or WaitHandleCannotBeOpenedException)
        {
            Log.Information("Update splash not shown: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>Called first thing at start: a splash that is waiting says Costas is opening.</summary>
    public static void SignalStarted() => Signal(SplashProtocol.StartedEvent);

    /// <summary>Called once the main window is on screen: a splash that is waiting closes.</summary>
    public static void SignalReady() => Signal(SplashProtocol.ReadyEvent);

    // Opening an event no splash created does nothing, so this is cheap at every start.
    private static void Signal(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            if (EventWaitHandle.TryOpenExisting(name, out var e))
            {
                using (e) e.Set();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            // Another user's splash, or none: nothing to tell.
        }
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
