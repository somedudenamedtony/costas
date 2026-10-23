// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Styling;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Data.Database;
using Ft8Client.Data.Settings;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Ft8Client.App.Tests;

public class AppearanceTests
{
    [Fact]
    public async Task Apply_SkinAndThemeChanges_UpdateOpenWindowsAndRestoreClassic()
    {
        // Dispatch directly: Avalonia.Headless.XUnit 12.1.3's discoverer targets an older xUnit API.
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AppearanceTests).Assembly);
        await session.Dispatch(CheckOpenWindows, CancellationToken.None);
    }

    private static void CheckOpenWindows()
    {
        var app = Application.Current!;
        var appearance = new AppearanceService(app);
        appearance.Apply(new AppearanceOptions { Theme = "light" });
        var originalAccent = Resource(app, "SystemAccentColor", ThemeVariant.Light);
        var originalBackground = Resource(app, "WindowBackgroundBrush", ThemeVariant.Light);
        var labelBrush = (IBrush?)Converters.KindBrush.Convert(TextKind.Accent, typeof(IBrush), null, CultureInfo.InvariantCulture);
        var chipBrush = (IBrush?)Converters.ShadeBackground.Convert(2, typeof(IBrush), null, CultureInfo.InvariantCulture);
        var main = new Window();
        var dialog = new Window();
        var card = new Border { Classes = { "card" } };
        main.Content = card;
        main.Show();
        dialog.Show();
        try
        {
            foreach (var skin in AppSkin.All.Skip(1))
            {
                foreach (var (mode, variant, palette) in new[]
                         { ("light", ThemeVariant.Light, skin.Light), ("dark", ThemeVariant.Dark, skin.Dark) })
                {
                    var critical = Resource(app, "CriticalBrush", variant);
                    var onAir = Resource(app, "OnAirBrush", variant);
                    appearance.Apply(new AppearanceOptions { Skin = skin.Id, Theme = mode });
                    main.UpdateLayout();
                    dialog.UpdateLayout();
                    ColorOf(main.Background).Should().Be(Color.Parse(palette.Window));
                    ColorOf(dialog.Background).Should().Be(Color.Parse(palette.Window));
                    ColorOf(main.Foreground).Should().Be(Color.Parse(palette.Text));
                    Resource(app, "SystemAccentColor", variant).Should().Be(Color.Parse(palette.Accent));
                    // These bindings still hold the brush returned before any skin changed.
                    ColorOf(labelBrush).Should().Be(Color.Parse(palette.AccentText));
                    ColorOf(chipBrush).Should().Be(Color.Parse(palette.Accent));
                    chipBrush!.Opacity.Should().Be(0.65);
                    card.CornerRadius.Should().Be(new CornerRadius(skin.Id switch { "radio" => 3, "retro" => 2, "vintage" => 14, "sdr" => 0, _ => 8 }));
                    Resource(app, "CriticalBrush", variant).Should().BeSameAs(critical);
                    Resource(app, "OnAirBrush", variant).Should().BeSameAs(onAir);
                }
            }

            // An unknown value behaves like Classic, including restoring Fluent's original system accent.
            appearance.Apply(new AppearanceOptions { Skin = "future-skin", Theme = "light" });
            main.UpdateLayout();
            main.Background.Should().BeSameAs(originalBackground);
            card.CornerRadius.Should().Be(new CornerRadius(8));
            Resource(app, "SystemAccentColor", ThemeVariant.Light).Should().Be(originalAccent);
            appearance.Apply(new AppearanceOptions { Theme = "system", Skin = "forest" });
            app.RequestedThemeVariant.Should().Be(ThemeVariant.Default);
        }
        finally
        {
            main.Close();
            dialog.Close();
            appearance.Apply(new AppearanceOptions());
        }
    }

    [Fact]
    public async Task Apply_InstrumentSkins_BindLiveMetersAndReuseTheDialEditor()
    {
        var dir = Path.Combine(Path.GetTempPath(), "costas-faceplates-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var settings = new AppSettings { SetupComplete = true };
        settings.Updates.CheckEnabled = false;
        settings.PskReporter.FeedEnabled = false;
        settings.Usage.Enabled = false;
        new SettingsStore(Path.Combine(dir, "settings.json")).Save(settings);
        SqliteDatabase? database = null;
        try
        {
            await using var host = await AppHost.StartAsync(new CommandLine { Home = dir, SimulateFolder = Path.Combine(TestPaths.Samples, "ft8") });
            database = host.Database;
            var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AppearanceTests).Assembly);
            await session.Dispatch(() =>
            {
                var appearance = new AppearanceService(Application.Current!);
                var vm = new MainViewModel(host);
                var window = new MainWindow { DataContext = vm };
                window.Show();
                try
                {
                    var originalDial = window.FindControl<Button>("DialFrequencyButton")!;
                    var face = window.FindControl<ContentControl>("InstrumentFace")!;
                    foreach (var skin in AppSkin.All.Where(s => s.StyleResource is not null))
                    {
                        appearance.Apply(new AppearanceOptions { Skin = skin.Id, Theme = "dark" });
                        window.Width = 1100;
                        window.Height = 720;
                        vm.SlotProgress = 0.42;
                        window.UpdateLayout();
                        face.IsVisible.Should().BeTrue();
                        originalDial.IsVisible.Should().BeFalse();
                        window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LeftScroll").Bounds.Height
                            .Should().BeGreaterThan(200, "the map below must leave room for contact controls");
                        var meter = face.GetVisualDescendants().OfType<SlotMeter>().Single();
                        meter.Value.Should().Be(0.42);
                        meter.Analog.Should().Be(skin.Id == "vintage");
                        if (skin.Id == "retro")
                        {
                            var display = face.GetVisualDescendants().OfType<SegmentedDisplay>().Single();
                            display.Text.Should().Be(vm.Frequency);
                            vm.Frequency = "50.313.000";
                            display.Text.Should().Be("50.313.000", "the segmented display must follow dial changes");
                        }
                        var dials = face.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("instrument-dial")).ToArray();
                        dials.Should().NotBeEmpty();
                        foreach (var dial in dials)
                        {
                            var at = dial.TranslatePoint(new Point(), face)!.Value;
                            (at.X + dial.Bounds.Width).Should().BeLessThanOrEqualTo(face.Bounds.Width + 1, "the dial must fit at minimum window width");
                        }
                        dials[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        originalDial.Flyout!.IsOpen.Should().BeTrue();
                        originalDial.Flyout.Hide();
                        vm.CanChangeBand = false;
                        dials.Should().OnlyContain(b => !b.IsEnabled, "frequency editing is locked out while transmitting");
                        vm.CanChangeBand = true;
                        SlotReadouts(window).Should().BeEmpty("the faceplate meter is the only slot countdown on Operate");
                        vm.Tab = 3;
                        window.UpdateLayout();
                        SlotReadouts(window).Should().BeEmpty("the faceplate meter is the only slot countdown on other tabs");
                        vm.Tab = 0;
                    }
                    appearance.Apply(new AppearanceOptions { Skin = "classic" });
                    window.UpdateLayout();
                    face.IsVisible.Should().BeFalse();
                    originalDial.IsVisible.Should().BeTrue();
                    SlotReadouts(window).Should().Equal("CardSlot");
                    vm.Tab = 3;
                    window.UpdateLayout();
                    SlotReadouts(window).Should().Equal("BarSlot");
                }
                finally
                {
                    window.Close();
                    appearance.Apply(new AppearanceOptions());
                }
            }, CancellationToken.None);
        }
        finally
        {
            // The host has stopped, but pooled SQLite handles still lock the file on Windows.
            // Release this test's pool only; other tests may be using their own databases in parallel.
            if (database is not null)
            {
                using var connection = database.Open();
                SqliteConnection.ClearPool(connection);
            }
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Settings_OldFileAndSkinRoundTrip_PreserveThemeAndUnits()
    {
        var dir = Path.Combine(Path.GetTempPath(), "costas-skins-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, """{"schema":2,"appearance":{"theme":"dark","units":"km"}}""");
            var store = new SettingsStore(path);
            var settings = store.Load();
            settings.Appearance.Skin.Should().Be("classic");
            foreach (var skin in AppSkin.All)
            {
                settings.Appearance.Skin = skin.Id;
                store.Save(settings);
                var loaded = store.Load().Appearance;
                loaded.Skin.Should().Be(skin.Id);
                loaded.Theme.Should().Be("dark");
                loaded.Units.Should().Be("km");
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static IEnumerable<string?> SlotReadouts(Window window) => window.GetVisualDescendants().OfType<StackPanel>()
        .Where(p => p.Name is "CardSlot" or "BarSlot" && p.IsEffectivelyVisible).Select(p => p.Name);

    private static object? Resource(Application app, string key, ThemeVariant variant)
    {
        app.TryGetResource(key, variant, out var value).Should().BeTrue(key);
        return value;
    }

    private static Color ColorOf(IBrush? brush) => ((ISolidColorBrush)brush!).Color;
}
