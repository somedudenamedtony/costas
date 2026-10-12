// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Headless;
using Avalonia.Threading;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;

namespace Ft8Client.App.Tests;

public class MainWindowDialogTests
{
    [Fact]
    public async Task OpenSettings_AfterMiniModeRoundTrip_OpensOneWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var home = Path.Combine(Path.GetTempPath(), "costas-test-" + Guid.NewGuid().ToString("N"));
        var host = await AppHost.StartAsync(CommandLine.Parse(["--home", home]));
        var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
        try
        {
            host.Settings.Update(s => s.SetupComplete = true);

            var settingsWindows = await session.Dispatch(() =>
            {
                var vm = new MainViewModel(host);
                var window = new MainWindow { DataContext = vm };
                window.Show();
                vm.OpenCommand.Execute("mini");
                vm.Mini.ExpandCommand.Execute(null);
                vm.OpenCommand.Execute("settings");
                Dispatcher.UIThread.RunJobs();
                var count = window.OwnedWindows.OfType<SettingsWindow>().Count();
                window.Close();
                return count;
            }, ct);

            settingsWindows.Should().Be(1);
        }
        finally
        {
            // The await above resumes on the session's own thread; disposing the session from there deadlocks.
            await Task.Run(session.Dispose, ct);
            await host.DisposeAsync();
            try { Directory.Delete(home, recursive: true); } catch (IOException) { }
        }
    }
}
