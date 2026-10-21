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
using Avalonia.Styling;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Data.Settings;
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
                    ColorOf(main.Background).Should().Be(Color.Parse(palette.Window));
                    ColorOf(dialog.Background).Should().Be(Color.Parse(palette.Window));
                    ColorOf(main.Foreground).Should().Be(Color.Parse(palette.Text));
                    Resource(app, "SystemAccentColor", variant).Should().Be(Color.Parse(palette.Accent));
                    // These bindings still hold the brush returned before any skin changed.
                    ColorOf(labelBrush).Should().Be(Color.Parse(palette.AccentText));
                    ColorOf(chipBrush).Should().Be(Color.Parse(palette.Accent));
                    chipBrush!.Opacity.Should().Be(0.65);
                    Resource(app, "CriticalBrush", variant).Should().BeSameAs(critical);
                    Resource(app, "OnAirBrush", variant).Should().BeSameAs(onAir);
                }
            }

            // An unknown value behaves like Classic, including restoring Fluent's original system accent.
            appearance.Apply(new AppearanceOptions { Skin = "future-skin", Theme = "light" });
            main.Background.Should().BeSameAs(originalBackground);
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

    private static object? Resource(Application app, string key, ThemeVariant variant)
    {
        app.TryGetResource(key, variant, out var value).Should().BeTrue(key);
        return value;
    }

    private static Color ColorOf(IBrush? brush) => ((ISolidColorBrush)brush!).Color;
}
