// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Data.Database;
using Ft8Client.Data.Settings;
using Microsoft.Data.Sqlite;

namespace Ft8Client.App.Tests;

public class PanelWindowTests
{
    [Fact]
    public void CanPop_EveryViewButOperate_True()
    {
        MainViewModel.CanPop(0).Should().BeFalse("Operate is the main window's job");
        Enumerable.Range(1, 4).Should().OnlyContain(t => MainViewModel.CanPop(t));
        MainViewModel.CanPop(5).Should().BeFalse();
    }

    [Fact]
    public async Task PopOut_ReachView_OpensItsOwnWindowAndRemembersIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "costas-panels-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var settings = new AppSettings { SetupComplete = true };
        settings.Updates.CheckEnabled = false;
        settings.PskReporter.FeedEnabled = false;
        settings.Usage.Enabled = false;
        settings.Dxpeditions.Enabled = false;
        new SettingsStore(Path.Combine(dir, "settings.json")).Save(settings);
        SqliteDatabase? database = null;
        try
        {
            await using var host = await AppHost.StartAsync(new CommandLine { Home = dir, SimulateFolder = Path.Combine(TestPaths.Samples, "ft8") });
            database = host.Database;
            var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PanelWindowTests).Assembly);
            await session.Dispatch(() =>
            {
                var vm = new MainViewModel(host);
                var window = new MainWindow { DataContext = vm };
                window.Show();
                try
                {
                    vm.Tab = 2;
                    vm.CanPopOut.Should().BeTrue();
                    vm.PopOutCommand.Execute(null);

                    var panel = window.OwnedWindows.OfType<PanelWindow>().Should().ContainSingle().Subject;
                    panel.Tab.Should().Be(2);
                    panel.Title.Should().Be("Costas · Reach");
                    panel.GetVisualDescendants().OfType<ReachView>().Should().ContainSingle();
                    vm.CurrentTabOut.Should().BeTrue();
                    vm.CanPopOut.Should().BeFalse();
                    host.Settings.Current.Window.Panels["Reach"].Out.Should().BeTrue();

                    vm.PopInCommand.Execute(null);
                    window.OwnedWindows.OfType<PanelWindow>().Should().BeEmpty();
                    vm.CurrentTabOut.Should().BeFalse();
                    host.Settings.Current.Window.Panels["Reach"].Out.Should().BeFalse();

                    vm.Tab = 3;
                    vm.PopOutCommand.Execute(null);
                    var log = window.OwnedWindows.OfType<PanelWindow>().Single();
                    log.Close();
                    vm.IsOut(3).Should().BeFalse("closing the window puts the view back on its tab");

                    vm.Tab = 4;
                    vm.PopOutCommand.Execute(null);
                }
                finally
                {
                    window.Close();
                }
                window.OwnedWindows.Should().BeEmpty();
                host.Settings.Current.Window.Panels["Awards"].Out.Should().BeTrue("a view still out when Costas closes opens out again next time");
            }, CancellationToken.None);
        }
        finally
        {
            if (database is not null)
            {
                using var connection = database.Open();
                SqliteConnection.ClearPool(connection);
            }
            AppearanceTests.DeleteWhenSettled(dir);
        }
    }
}
