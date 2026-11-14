// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.Services;
using Ft8Client.UpdateSplash;

namespace Ft8Client.App.Tests;

public sealed class UpdateSplashLauncherTests
{
    [Fact]
    public async Task ShowAsync_NoSplashBesideTheApp_ReturnsNullAndUpdateGoesAhead()
    {
        var dir = Path.Combine(Path.GetTempPath(), "costas-splash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var splash = await UpdateSplashLauncher.ShowAsync(dir, Path.Combine(dir, "copies"), new SplashOptions(), TimeSpan.FromMilliseconds(100));

            splash.Should().BeNull();
            Directory.Exists(Path.Combine(dir, "copies")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SignalStartedAndReady_NoSplashRunning_DoNothing()
    {
        // Every start calls these; with no splash waiting they must be harmless (and on Linux, no-ops).
        var act = () =>
        {
            UpdateSplashLauncher.SignalStarted();
            UpdateSplashLauncher.SignalReady();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void SplashOptions_InTheApp_MatchTheSplashProgramsCommandLine()
    {
        // The app compiles the splash's SplashOptions in (linked); this is what it passes.
        var args = new SplashOptions { ProcessId = 12, Version = "0.1.5", Center = (10, 20) }.ToArguments();

        args.Should().StartWith(["--pid", "12", "--version", "0.1.5", "--center", "10,20"]);
        SplashProtocol.ExeName.Should().Be("CostasUpdateSplash.exe");
    }
}
