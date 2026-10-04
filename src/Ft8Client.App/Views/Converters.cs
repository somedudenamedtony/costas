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

    /// <summary>Step look to background.</summary>
    public static readonly IValueConverter StepBackground = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Done => Brush("SelectedRowBrush"),
        StepLook.Transmitting => Brush("CriticalBrush"),
        _ => Brushes.Transparent,
    });

    /// <summary>Step look to border.</summary>
    public static readonly IValueConverter StepBorder = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Done or StepLook.Waiting => Brush("AccentBrush"),
        StepLook.Transmitting => Brush("CriticalBrush"),
        _ => Brushes.Transparent,
    });

    /// <summary>True for upcoming steps (dashed outline).</summary>
    public static readonly IValueConverter IsUpcoming = new FuncValueConverter<StepLook, bool>(l => l == StepLook.Upcoming);

    /// <summary>Step look to text.</summary>
    public static readonly IValueConverter StepForeground = new FuncValueConverter<StepLook, IBrush?>(l => l switch
    {
        StepLook.Transmitting => Brushes.White,
        StepLook.Upcoming => Brush("TextSecondaryBrush"),
        _ => Brush("TextPrimaryBrush"),
    });

    /// <summary>Dashed border for upcoming steps.</summary>
    public static readonly IValueConverter StepDash = new FuncValueConverter<StepLook, Avalonia.Collections.AvaloniaList<double>?>(l =>
        l == StepLook.Upcoming ? new Avalonia.Collections.AvaloniaList<double>([4, 3]) : null);

    /// <summary>Offset to a top margin (mine raised, theirs lowered).</summary>
    public static readonly IValueConverter StepMargin = new FuncValueConverter<double, Thickness>(o => new Thickness(0, 12 + o, 6, 12 - o));

    /// <summary>Selected row background.</summary>
    public static readonly IValueConverter SelectedBackground = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("SelectedRowBrush") : Brushes.Transparent);

    /// <summary>Selected row left bar.</summary>
    public static readonly IValueConverter SelectedBar = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("AccentBrush") : Brushes.Transparent);

    /// <summary>True when an int equals the parameter.</summary>
    public static readonly IValueConverter IntEquals = new IntEqualsConverter();

    /// <summary>True when a string is not empty.</summary>
    public static readonly IValueConverter NotEmpty = new FuncValueConverter<string?, bool>(s => !string.IsNullOrEmpty(s));

    /// <summary>Transmitting colours the Halt button.</summary>
    public static readonly IValueConverter CriticalWhen = new FuncValueConverter<bool, IBrush?>(b => b ? Brush("CriticalBrush") : null);

    /// <summary>Progress bar colour.</summary>
    public static readonly IValueConverter ProgressBrush = new FuncValueConverter<bool, IBrush?>(b => Brush(b ? "CriticalBrush" : "AccentBrush"));

    private static IBrush? Brush(string key)
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
