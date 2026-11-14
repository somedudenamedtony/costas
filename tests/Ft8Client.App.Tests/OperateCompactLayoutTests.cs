// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Ranking;
using Ft8Client.Data.Database;
using Ft8Client.Data.Settings;
using Microsoft.Data.Sqlite;

namespace Ft8Client.App.Tests;

/// <summary>The compact Operate layout for small screens: the idle strip, the side column's tabs and its fold.</summary>
public class OperateCompactLayoutTests
{
    private static readonly DateTime Slot = new(2026, 11, 14, 20, 23, 15, DateTimeKind.Utc);

    public class NoCommands : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static BandSummary Band() => new()
    {
        Band = "20m", StationsHeard = 212, Countries = 14, CallingCq = 31, YouNeed = 198,
        HeardFrom = new Dictionary<string, int> { ["NA"] = 199, ["EU"] = 8, ["AF"] = 2 },
        LogCountriesWorked = 19, StatesWorked = 46, StatesMissing = ["CT", "MD", "NY"], StatesMoreMissing = 1, GridsWorked = 142,
    };

    private static ContactView Contact() => new("C5R", "IK13", ContactFlow.Hound, ContactOutcome.InProgress, null, null,
    [
        new ContactStep(true, "C5R W7LIT DN40", StepState.Done, 1, Slot.AddSeconds(-15)),
        new ContactStep(false, "W7LIT C5R ±NN", StepState.Now, 0, null),
    ], false, false, string.Empty);

    private static OperateViewModel Vm(bool inContact = false)
    {
        var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => Slot.AddSeconds(7));
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8, Summary = Band(), Contact = inContact ? Contact() : null }, [], 0);
        return vm;
    }

    private static Task InWindow(OperateViewModel vm, double width, double height, Action<Window, Action> check)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(OperateCompactLayoutTests).Assembly);
        return session.Dispatch(() =>
        {
            var window = new Window { Width = width, Height = height, Content = new OperateView { DataContext = vm } };
            window.Show();
            try
            {
                void Settle()
                {
                    for (var i = 0; i < 3; i++)
                    {
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                    }
                }

                Settle();
                check(window, Settle);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static T Named<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [Fact]
    public void Summary_Strip_SaysTheBandAndTheLogInOneLine()
    {
        var s = new SummaryViewModel(Band(), false);

        s.StripTitle.Should().Be("20 m now");
        s.LogLine.Should().Be("19 countries · 46/50 states · 142 grids");
        s.LogTip.Should().Contain("need CT, MD, NY and 1 more", "the states still needed stay one hover away");
        s.Continents.Select(c => c.Code).Should().Equal("NA", "EU", "AS", "OC", "SA", "AF");
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 0.24, 2)]
    [InlineData(199, 48, 14)]
    [InlineData(100, 24, 7)]
    public void ContinentBar_MiniHeight_ScalesAndNeverHidesAStation(int count, double barHeight, double expected) =>
        new ContinentBar("Europe", count, barHeight, "EU").MiniHeight.Should().Be(expected);

    [Fact]
    public void ContinentBar_Tip_NamesTheContinentAndCount()
    {
        new ContinentBar("Europe", 8, 2, "EU").Tip.Should().Be("Europe: 8 stations");
        new ContinentBar("S. Am.", 1, 1, "SA").Tip.Should().Be("S. Am.: 1 station");
    }

    [Fact]
    public void Panel_IdleCards_AreIdleAndAContactIsNot()
    {
        ContactPanelViewModel.Idle.IsIdle.Should().BeTrue();
        ContactPanelViewModel.IdleCallingCq.IsIdle.Should().BeTrue();
        ContactPanelViewModel.IdleAutoCall.IsIdle.Should().BeTrue();
        Vm(inContact: true).Panel.IsIdle.Should().BeFalse();
    }

    [Fact]
    public void ShowStrip_Idle_ShowsUnlessTheBandTabAlreadyShowsTheSummary()
    {
        var vm = Vm();

        vm.SideTab.Should().Be(OperateViewModel.HeardTab, "the map is the tab a new install opens on");
        vm.ShowStrip.Should().BeTrue();

        vm.SideTab = OperateViewModel.BandTab;
        vm.ShowStrip.Should().BeFalse("the full summary is on screen, and nothing is shown twice");

        vm.SideCollapsed = true;
        vm.ShowStrip.Should().BeTrue("folded, the Band tab is out of sight");
    }

    [Fact]
    public void ShowStrip_InContactOrNoSummary_Hides()
    {
        Vm(inContact: true).ShowStrip.Should().BeFalse();
        var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => Slot);
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8 }, [], 0);
        vm.ShowStrip.Should().BeFalse();
    }

    [Fact]
    public void ShowSide_FromTheRail_OpensTheColumnOnThatTab()
    {
        var vm = Vm();
        vm.SideCollapsed = true;
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ShowSideCommand.Execute("0");

        vm.SideCollapsed.Should().BeFalse();
        vm.SideTab.Should().Be(OperateViewModel.BandTab);
        changed.Should().Contain(nameof(OperateViewModel.ShowStrip));
    }

    [Fact]
    public void ToggleSide_FoldsAndOpens()
    {
        var vm = Vm();
        vm.ToggleSideCommand.Execute(null);
        vm.SideCollapsed.Should().BeTrue();
        vm.ToggleSideCommand.Execute(null);
        vm.SideCollapsed.Should().BeFalse();
    }

    [Fact]
    public void Rail_SaysTheBandAndWhatIsNeeded()
    {
        Vm().RailBand.Should().Be("20 m · 198 you need");
        Vm().RailSignal.Should().Be("Your signal");
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1536, 864)]
    public Task View_IdleOnALaptop_CardIsTwoLinesAndTheCallListGetsTheRoom(double width, double height) =>
        InWindow(Vm(), width, height, (window, _) =>
        {
            var card = Named<Border>(window, "ContactCard");
            var strip = Named<Border>(window, "BandStrip");
            var list = Named<ScrollViewer>(window, "LeftScroll");

            strip.IsEffectivelyVisible.Should().BeTrue();
            strip.GetVisualAncestors().Should().Contain(card, "the strip is part of the pinned card, so it never scrolls away");
            card.Bounds.Height.Should().BeLessThanOrEqualTo(100, "idle, the card is one line and the strip");
            Named<StackPanel>(window, "ContactButtons").IsEffectivelyVisible.Should().BeFalse("Resend, Log now and Abandon have nothing to act on");
            Named<StackPanel>(window, "CallRow").IsEffectivelyVisible.Should().BeFalse();
            window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Auto-call").IsEffectivelyVisible.Should().BeTrue();
            list.Bounds.Height.Should().BeGreaterThanOrEqualTo(height - 130, "the call list has nearly the whole height");
        });

    [Fact]
    public Task View_InContact_CardIsWholeAndTheStripIsGone() =>
        InWindow(Vm(inContact: true), 1280, 720, (window, _) =>
        {
            Named<Border>(window, "BandStrip").IsEffectivelyVisible.Should().BeFalse();
            Named<StackPanel>(window, "ContactButtons").IsEffectivelyVisible.Should().BeTrue();
            Named<StackPanel>(window, "CallRow").IsEffectivelyVisible.Should().BeTrue();
            var onAir = Named<Border>(window, "OnAir");
            onAir.BorderThickness.Should().Be(new Thickness(1));
            onAir.Padding.Should().Be(new Thickness(14, 10));
            Named<Border>(window, "ContactCard").Padding.Should().Be(new Thickness(18, 12));
            window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("houndBanner")).Should().ContainSingle()
                .Which.IsEffectivelyVisible.Should().BeTrue("the Hound banner keeps its place in the full card");
        });

    [Fact]
    public Task View_BandTab_HoldsTheFullSummaryAndHidesTheStrip() =>
        InWindow(Vm(), 1280, 720, (window, settle) =>
        {
            var vm = (OperateViewModel)window.GetVisualDescendants().OfType<OperateView>().Single().DataContext!;
            var tabs = Named<TabControl>(window, "SideTabs");
            tabs.Items.OfType<TabItem>().Select(t => t.Header).Should().Equal("Band", "Heard", "Signal", "Session");
            tabs.SelectedIndex.Should().Be(OperateViewModel.HeardTab);

            vm.ShowSideCommand.Execute("0");
            settle();

            tabs.SelectedIndex.Should().Be(OperateViewModel.BandTab);
            window.GetVisualDescendants().OfType<StackPanel>().Where(p => AutomationProperties.GetName(p) == "Band summary").Should().ContainSingle()
                .Which.IsEffectivelyVisible.Should().BeTrue();
            Named<Border>(window, "BandStrip").IsEffectivelyVisible.Should().BeFalse();

            tabs.SelectedIndex = OperateViewModel.SignalTab;
            settle();
            vm.SideTab.Should().Be(OperateViewModel.SignalTab, "the tab clicked is the tab remembered");
            Named<Border>(window, "BandStrip").IsEffectivelyVisible.Should().BeTrue();
        });

    [Fact]
    public Task View_Folded_RailIsThinAndTheCallListTakesTheWidth() =>
        InWindow(Vm(), 1280, 720, (window, settle) =>
        {
            var vm = (OperateViewModel)window.GetVisualDescendants().OfType<OperateView>().Single().DataContext!;
            var list = Named<ScrollViewer>(window, "LeftScroll");
            var open = list.Bounds.Width;

            vm.SideCollapsed = true;
            settle();

            Named<Panel>(window, "RightColumn").Bounds.Width.Should().Be(OperateView.RailWidth);
            Named<Border>(window, "SideRail").IsEffectivelyVisible.Should().BeTrue();
            Named<Border>(window, "SideCard").IsEffectivelyVisible.Should().BeFalse();
            list.Bounds.Width.Should().BeApproximately(open + 300 - OperateView.RailWidth, 1);

            vm.ShowSideCommand.Execute("2");
            settle();
            Named<Panel>(window, "RightColumn").Bounds.Width.Should().Be(300);
            Named<TabControl>(window, "SideTabs").SelectedIndex.Should().Be(OperateViewModel.SignalTab);
        });

    [Fact]
    public Task View_FoldedOnANarrowWindow_RailLiesUnderTheList() =>
        InWindow(Vm(), 1000, 700, (window, settle) =>
        {
            var vm = (OperateViewModel)window.GetVisualDescendants().OfType<OperateView>().Single().DataContext!;
            vm.SideCollapsed = true;
            settle();

            var rail = Named<Border>(window, "SideRail");
            rail.Bounds.Height.Should().BeLessThanOrEqualTo(48, "under the list the rail is one line, not a column");
            Named<StackPanel>(window, "RailStack").Orientation.Should().Be(Avalonia.Layout.Orientation.Horizontal);
        });

    [Fact]
    public async Task MainWindow_SideColumnAndUpdateBar_RememberedAndSlim()
    {
        var dir = Path.Combine(Path.GetTempPath(), "costas-compact-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var settings = new AppSettings { SetupComplete = true };
        settings.Updates.CheckEnabled = false;
        settings.PskReporter.FeedEnabled = false;
        settings.Usage.Enabled = false;
        settings.Dxpeditions.Enabled = false;
        settings.Window.Width = 1280;
        settings.Window.Height = 720;
        settings.Window.SideTab = OperateViewModel.SignalTab;
        new SettingsStore(Path.Combine(dir, "settings.json")).Save(settings);
        SqliteDatabase? database = null;
        try
        {
            await using var host = await AppHost.StartAsync(new CommandLine { Home = dir, SimulateFolder = Path.Combine(TestPaths.Samples, "ft8") });
            database = host.Database;
            var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(OperateCompactLayoutTests).Assembly);
            await session.Dispatch(() =>
            {
                var vm = new MainViewModel(host);
                var window = new MainWindow { DataContext = vm };
                window.Show();
                try
                {
                    vm.Operate.SideTab.Should().Be(OperateViewModel.SignalTab, "the tab left showing opens again");

                    vm.Operate.ToggleSideCommand.Execute(null);
                    vm.Operate.SideTab = OperateViewModel.BandTab;
                    host.Settings.Current.Window.SideCollapsed.Should().BeTrue();
                    host.Settings.Current.Window.SideTab.Should().Be(OperateViewModel.BandTab);

                    vm.Update.Text = "Version 9.9.9 is available.";
                    vm.Update.Visible = true;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var bar = Named<Border>(window, "UpdateBar");
                    bar.IsEffectivelyVisible.Should().BeTrue();
                    bar.Bounds.Height.Should().BeLessThanOrEqualTo(32, "the update bar is one slim line");
                }
                finally
                {
                    window.Close();
                }
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
