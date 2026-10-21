// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Ft8Client.App.Views;

/// <summary>Live resource brushes for converter bindings whose input does not change with the skin.</summary>
internal static class ThemeBrush
{
    // One subscription per token/opacity for the lifetime of the application, rather than one per station row.
    private static readonly ConditionalWeakTable<Application,
        Dictionary<(string Key, double Opacity), (SolidColorBrush Brush, IDisposable Subscription)>> Cache = new();

    public static IBrush? Get(string key, double opacity = 1)
    {
        var app = Application.Current;
        if (app is null) return null;
        var brushes = Cache.GetOrCreateValue(app);
        if (brushes.TryGetValue((key, opacity), out var cached)) return cached.Brush;
        var brush = new SolidColorBrush { Opacity = opacity };
        var subscription = app.GetResourceObservable(key).Subscribe(new BrushObserver(brush));
        brushes[(key, opacity)] = (brush, subscription);
        return brush;
    }

    private sealed class BrushObserver(SolidColorBrush brush) : IObserver<object?>
    {
        public void OnNext(object? value) => brush.Color = (value as ISolidColorBrush)?.Color ?? Colors.Transparent;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
