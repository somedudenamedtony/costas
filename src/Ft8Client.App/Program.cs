// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Serilog;

namespace Ft8Client.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var cmd = CommandLine.Parse(args);
        try
        {
            App.Host = AppHost.StartAsync(cmd).GetAwaiter().GetResult();
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
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
            Log.CloseAndFlush();
        }
    }

    /// <summary>Used by the designer and by Main.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
