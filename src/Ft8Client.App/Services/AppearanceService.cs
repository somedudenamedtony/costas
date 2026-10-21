// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Services;

/// <summary>Applies skin tokens and Fluent palettes to every desktop window on the UI thread.</summary>
internal sealed class AppearanceService
{
    private readonly Application _app;
    private readonly FluentTheme _fluent;
    private readonly ResourceDictionary _light;
    private readonly ResourceDictionary _dark;
    private string? _skin;

    public AppearanceService(Application app)
    {
        _app = app;
        _fluent = app.Styles.OfType<FluentTheme>().Single();
        _light = (ResourceDictionary)app.Resources.ThemeDictionaries[ThemeVariant.Light];
        _dark = (ResourceDictionary)app.Resources.ThemeDictionaries[ThemeVariant.Dark];
    }

    public void Apply(AppearanceOptions options)
    {
        var skin = AppSkin.Find(options.Skin);
        if (_skin != skin.Id)
        {
            ApplyPalette(ThemeVariant.Light, _light, skin.Id == "classic" ? null : skin.Light);
            ApplyPalette(ThemeVariant.Dark, _dark, skin.Id == "classic" ? null : skin.Dark);
            _skin = skin.Id;
        }
        var theme = options.Theme switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        if (_app.RequestedThemeVariant != theme) _app.RequestedThemeVariant = theme;
    }

    private void ApplyPalette(ThemeVariant variant, ResourceDictionary original, SkinPalette? palette)
    {
        if (palette is null)
        {
            _app.Resources.ThemeDictionaries[variant] = original;
            _fluent.Palettes.Remove(variant);
            return;
        }

        // Fall through to the original dictionary for safety/status colors. Skin choices only change surfaces,
        // text and accent; red transmit, caution and success retain their established meanings.
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(original);
        resources["WindowBackgroundBrush"] = Brush.Parse(palette.Window);
        resources["CardBackgroundBrush"] = Brush.Parse(palette.Card);
        resources["CardBorderBrush"] = Brush.Parse(palette.Border);
        resources["ControlBorderBrush"] = Brush.Parse(palette.ControlBorder);
        resources["TextPrimaryBrush"] = Brush.Parse(palette.Text);
        resources["TextSecondaryBrush"] = Brush.Parse(palette.Secondary);
        resources["AccentBrush"] = Brush.Parse(palette.Accent);
        resources["AccentTextBrush"] = Brush.Parse(palette.AccentText);
        resources["SelectedRowBrush"] = Brush.Parse(palette.Selection);
        resources["RowDividerBrush"] = Brush.Parse(palette.Divider);
        _app.Resources.ThemeDictionaries[variant] = resources;

        // Use Fluent's palette API so stock buttons, checks, menus and selection states share the skin's accent.
        _fluent.Palettes[variant] = new ColorPaletteResources
        {
            Accent = Color.Parse(palette.Accent),
            RegionColor = Color.Parse(palette.Window),
        };
    }
}
