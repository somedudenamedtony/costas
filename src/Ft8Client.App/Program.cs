// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Ft8Client.App.Services;
using Serilog;
using Velopack;

namespace Ft8Client.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // First, before anything else: Velopack's installer and updater start the app with their own arguments to run
        // install and update steps, and this handles those and exits. Otherwise it returns at once.
        VelopackApp.Build().Run();
        // After an update: the splash that covered it says Costas is opening (it closes when the window is up).
        UpdateSplashLauncher.SignalStarted();
        var cmd = CommandLine.Parse(args);
        // An exception on any other thread (capture, decoding, a timer) ends the process without passing the catch
        // below, and a radio keyed over CAT would stay keyed with nobody left to unkey it.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LastChance(e.ExceptionObject as Exception);
        try
        {
            App.Host = AppHost.StartAsync(cmd).GetAwaiter().GetResult();
            var code = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            // The app has shut down (radio, jt9 and log closed), so an update that installs elsewhere can start Costas again.
            if (UpdateService.RunAfterExit is { } next) System.Diagnostics.Process.Start(next)?.Dispose();
            return code;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
            Ft8Client.Integrations.Crash.CrashReporter.Capture(ex);
            try
            {
                App.Host?.Transmitter?.HaltAsync().GetAwaiter().GetResult();
            }
            catch (Exception haltEx)
            {
                Log.Error(haltEx, "PTT release after crash failed");
            }
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            // Sends a crash report that is waiting (briefly, and only when online); the cache keeps it otherwise.
            App.Host?.Crashes?.Dispose();
            Log.CloseAndFlush();
        }
    }

    private static int s_lastChance;

    // The process is about to end: release PTT while rigctld is still there to do it, and get the error into the log.
    private static void LastChance(Exception? ex)
    {
        if (Interlocked.Exchange(ref s_lastChance, 1) != 0) return;
        try
        {
            Log.Fatal(ex, "Unhandled exception on a background thread");
            if (App.Host?.Transmitter is { } tx && !tx.HaltAsync().Wait(TimeSpan.FromSeconds(3)))
                Log.Error("PTT release after crash did not finish in 3 s");
            if (ex is not null) Ft8Client.Integrations.Crash.CrashReporter.Capture(ex);
        }
        catch (Exception haltEx)
        {
            Log.Error(haltEx, "PTT release after crash failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>Used by the designer and by Main.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
