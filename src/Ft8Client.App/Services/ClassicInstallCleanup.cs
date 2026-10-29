// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Serilog;
using Velopack.Windows;

namespace Ft8Client.App.Services;

/// <summary>
/// After a classic (Inno Setup) install has moved to a Velopack install, removes the classic copy so only one Costas is
/// left in the Start menu and Apps &amp; features. Only the program folder goes: settings, log and recordings live in the
/// data folder, which neither installer touches.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ClassicInstallCleanup
{
    // The classic installer's AppId (installer/Costas.iss); Inno Setup adds "_is1".
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ED0829A3-63BC-4E6C-99CB-5DFF4BB4A021}_is1";

    private static readonly string[] OwnPrograms = ["Costas", "jt9", "rigctld"];

    /// <summary>
    /// Runs the classic uninstaller silently when a per-user classic install is still there, then puts back the Start menu
    /// (and desktop) shortcut it removes, which has the same name as Velopack's. An all-users install would need an
    /// administrator prompt at start-up, so it is left and logged.
    /// </summary>
    public static void Run(string appDir)
    {
        try
        {
            if (Registry.LocalMachine.OpenSubKey(UninstallKey) is { } machine)
            {
                machine.Dispose();
                Log.Information("A classic all-users Costas install is still present; remove it from Apps & features");
            }
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            if (key?.GetValue("InstallLocation") is not string location || key.GetValue("UninstallString") is not string uninstall) return;
            var folder = Path.GetFullPath(location);
            if (Path.GetFullPath(appDir).StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return;
            var exe = uninstall.Trim().Trim('"');
            if (!File.Exists(exe)) return;
            WaitForExit(folder, TimeSpan.FromSeconds(60));
            var desktop = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Costas.lnk"));
            Log.Information("Removing the classic install in {Folder}", folder);
            using (var p = Process.Start(new ProcessStartInfo(exe, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = false }))
            {
                if (p is not null && !p.WaitForExit(TimeSpan.FromMinutes(2))) Log.Warning("The classic uninstaller is still running after 2 minutes");
            }
            // Inno's uninstaller hands over to a copy of itself in the temp folder; give it a moment to finish.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (Directory.Exists(Path.Combine(folder, "third_party")) && DateTime.UtcNow < deadline) Thread.Sleep(500);
            var locations = desktop ? ShortcutLocation.StartMenuRoot | ShortcutLocation.Desktop : ShortcutLocation.StartMenuRoot;
            // Velopack makes its shortcuts at install time only, and the classic uninstaller has just deleted the one with
            // the same name, so it is made again here.
#pragma warning disable CS0618
            new Shortcuts().CreateShortcutForThisExe(locations);
#pragma warning restore CS0618
            Log.Information("Classic install removed");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                                       or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            Log.Warning(ex, "Removing the classic install failed");
        }
    }

    // The classic Costas that started the move may still be shutting down (it stops jt9 and rigctld first).
    private static void WaitForExit(string folder, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < deadline && RunningFrom(folder)) Thread.Sleep(500);
    }

    private static bool RunningFrom(string folder)
    {
        foreach (var name in OwnPrograms)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    try
                    {
                        if (p.MainModule?.FileName is { } path && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                    {
                        // Another user's process, or it just exited.
                    }
                }
            }
        }
        return false;
    }
}
