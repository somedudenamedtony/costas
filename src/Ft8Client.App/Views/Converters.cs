// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Services;

namespace Ft8Client.App.Views;

/// <summary>Value converters used by the views.</summary>
public static class Converters
{
    /// <summary>TextKind to a design-token brush.</summary>
    public static readonly IValueConverter KindBrush = new FuncValueConverter<TextKind, IBrush?>(k => Brush(k switch
    {
        TextKind.Secondary => "TextSecondaryBrush",
        TextKind.Accent => "AccentTextBrush",
        TextKind.Caution => "CautionBrush",
        TextKind.Critical => "CriticalBrush",
        TextKind.Success => "SuccessBrush",
        _ => "TextPrimaryBrush",
    }));

    /// <summary>Raw decodes line text: another program's highlight colour, or the line's own colour.</summary>
    public static readonly IValueConverter RawForeground = new FuncValueConverter<RawLineViewModel, IBrush?>(l =>
        l?.Foreground is { } c ? new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)) : KindBrush.Convert(l?.Kind ?? TextKind.Normal, typeof(IBrush), null, CultureInfo.InvariantCulture) as IBrush);

    /// <summary>Raw decodes line background: another program's highlight colour, or none.</summary>
    public static readonly IValueConverter RawBackground = new FuncValueConverter<RawLineViewModel, IBrush?>(l =>
        l?.Background is { } c ? new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)) : Brushes.Transparent);

    /// <summary>Legend dot fill: the kind's colour, or none for a ring.</summary>
    public static readonly IValueConverter LegendFill = new FuncValueConverter<LegendItem, IBrush?>(l => l is null || l.Ring ? null : Brush(StationMap.DotBrushKey(l.Kind)));

    /// <summary>Legend dot outline: the kind's colour for a ring, none for a dot.</summary>
    public static readonly IValueConverter LegendStroke = new FuncValueConverter<LegendItem, IBrush?>(l => l is { Ring: true } ? Brush(StationMap.DotBrushKey(l.Kind)) : null);

    /// <summary>Service health to a dot brush.</summary>
    public static readonly IValueConverter HealthBrush = new FuncValueConverter<ServiceHealth, IBrush?>(h => Brush(h switch
    {
        ServiceHealth.Ok => "SuccessBrush",
        ServiceHealth.Degraded => "CautionBrush",
        ServiceHealth.Down => "CriticalBrush",
        _ => "TextSecondaryBrush",
    }));

    /// <summary>Bold for true.</summary>
    public static readonly IValueConverter Bold = new FuncValueConverter<bool, FontWeight>(b => b ? FontWeight.SemiBold : FontWeight.Normal);

    /// <summary>Semibold for false (the other station's call stands out from "You").</summary>
    public static readonly IValueConverter NotBold = new FuncValueConverter<bool, FontWeight>(b => b ? FontWeight.Normal : FontWeight.SemiBold);

    /// <summary>Step look to box background: red while on the air, tinted while waiting.</summary>
    public static readonly IValueConverter StepBackground = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Transmitting => Brush("CriticalBrush"),
        StepLook.Waiting => Brush("SelectedRowBrush"),
        _ => Brushes.Transparent,
    });

    /// <summary>Step look to box border (upcoming boxes draw a dashed outline instead).</summary>
    public static readonly IValueConverter StepBorder = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Transmitting => Brush("CriticalBrush"),
        StepLook.Waiting => Brush("AccentBrush"),
        StepLook.Done => Brush("ControlBorderBrush"),
        _ => Brushes.Transparent,
    });

    /// <summary>Step look to border thickness: the step being waited for stands out.</summary>
    public static readonly IValueConverter StepBorderThickness = new FuncValueConverter<StepLook, Thickness>(l => new Thickness(l == StepLook.Waiting ? 2 : 1));

    /// <summary>True for upcoming steps (dashed outline).</summary>
    public static readonly IValueConverter IsUpcoming = new FuncValueConverter<StepLook, bool>(l => l == StepLook.Upcoming);

    /// <summary>Step look to text.</summary>
    public static readonly IValueConverter StepForeground = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Transmitting => Brushes.White,
        StepLook.Upcoming => Brush("TextSecondaryBrush"),
        _ => Brush("TextPrimaryBrush"),
    });

    /// <summary>Step look to the status line.</summary>
    public static readonly IValueConverter StepStatusForeground = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Transmitting => Brushes.White,
        StepLook.Waiting => Brush("AccentTextBrush"),
        _ => Brush("TextSecondaryBrush"),
    });

    /// <summary>Dashed border for upcoming steps.</summary>
    public static readonly IValueConverter StepDash = new FuncValueConverter<StepLook, Avalonia.Collections.AvaloniaList<double>?>(l =>
        l == StepLook.Upcoming ? new Avalonia.Collections.AvaloniaList<double>([4, 3]) : null);

    /// <summary>Offset to a top margin (mine raised, theirs lowered).</summary>
    public static readonly IValueConverter StepMargin = new FuncValueConverter<double, Thickness>(o => new Thickness(0, 12 + o, 6, 12 - o));

    /// <summary>Progress marker fill: solid once done or on the air, a ring otherwise.</summary>
    public static readonly IValueConverter MarkerFill = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Done => Brush("AccentBrush"),
        StepLook.Transmitting => Brush("CriticalBrush"),
        _ => Brush("CardBackgroundBrush"),
    });

    /// <summary>Progress marker ring.</summary>
    public static readonly IValueConverter MarkerStroke = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Done or StepLook.Waiting => Brush("AccentBrush"),
        StepLook.Transmitting => Brush("CriticalBrush"),
        _ => Brush("ControlBorderBrush"),
    });

    /// <summary>Progress marker text.</summary>
    public static readonly IValueConverter MarkerForeground = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Done or StepLook.Transmitting => Brushes.White,
        StepLook.Waiting => Brush("AccentTextBrush"),
        _ => Brush("TextSecondaryBrush"),
    });

    /// <summary>My own overs in the conversation box are dimmed.</summary>
    public static readonly IValueConverter MineOpacity = new FuncValueConverter<bool, double>(mine => mine ? 0.6 : 1.0);

    /// <summary>Progress track: accent where reached, a plain line ahead.</summary>
    public static readonly IValueConverter TrackBrush = new FuncValueConverter<bool, IBrush?>(b => Brush(b ? "AccentBrush" : "ControlBorderBrush"));

    /// <summary>Selected row background.</summary>
    public static readonly IValueConverter SelectedBackground = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("SelectedRowBrush") : Brushes.Transparent);

    /// <summary>Selected row left bar.</summary>
    public static readonly IValueConverter SelectedBar = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("AccentBrush") : Brushes.Transparent);

    /// <summary>Outline of the row the operator picked, kept apart from the in-contact tint.</summary>
    public static readonly IValueConverter CursorOutline = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("TextSecondaryBrush") : Brushes.Transparent);

    /// <summary>True when an int equals the parameter.</summary>
    public static readonly IValueConverter IntEquals = new IntEqualsConverter();

    /// <summary>True when a string is not empty.</summary>
    public static readonly IValueConverter NotEmpty = new FuncValueConverter<string?, bool>(s => !string.IsNullOrEmpty(s));

    /// <summary>Caution brush when true, else the default foreground.</summary>
    public static readonly IValueConverter CautionWhen = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("CautionBrush") : null);

    /// <summary>Transmitting colours the Halt button.</summary>
    public static readonly IValueConverter CriticalWhen = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("CriticalBrush") : null);

    /// <summary>Critical text while transmitting, primary otherwise.</summary>
    public static readonly IValueConverter TransmittingText = new FuncValueConverter<bool, IBrush?>(b => Brush(b ? "CriticalBrush" : "TextPrimaryBrush"));

    /// <summary>Progress bar colour.</summary>
    public static readonly IValueConverter ProgressBrush = new FuncValueConverter<bool, IBrush?>(b => Brush(b ? "CriticalBrush" : "AccentBrush"));

    /// <summary>Band menu continent chip shade (0 to 3) to its fill: none, then the accent colour at rising strength.</summary>
    public static readonly IValueConverter ShadeBackground = new FuncValueConverter<int, IBrush?>(l =>
        l <= 0 || Brush("AccentBrush") is not ISolidColorBrush a ? Brushes.Transparent : new SolidColorBrush(a.Color, l switch { 1 => 0.3, 2 => 0.65, _ => 1.0 }));

    /// <summary>Band menu continent chip shade to its outline: the control border when there are no spots.</summary>
    public static readonly IValueConverter ShadeBorder = new FuncValueConverter<int, IBrush?>(l => l <= 0 ? Brush("ControlBorderBrush") : Brushes.Transparent);

    /// <summary>Band menu continent chip shade to its text.</summary>
    public static readonly IValueConverter ShadeForeground = new FuncValueConverter<int, IBrush?>(l => l switch
    {
        <= 0 => Brush("TextSecondaryBrush"),
        1 => Brush("TextPrimaryBrush"),
        _ => Brushes.White,
    });

    /// <summary>A theme brush by key, for the current theme variant; null when there is none.</summary>
    internal static IBrush? Brush(string key)
    {
        var app = Application.Current;
        if (app is null) return null;
        return app.TryGetResource(key, app.ActualThemeVariant, out var v) ? v as IBrush : null;
    }

    private sealed class IntEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int i && int.TryParse(parameter as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && i == p;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true && int.TryParse(parameter as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : Avalonia.Data.BindingOperations.DoNothing;
    }
}
