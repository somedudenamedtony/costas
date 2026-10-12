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
using Ft8Client.Core.Geo;

namespace Ft8Client.App.Views;

/// <summary>Five small cells, filled from the left in the caution colour up to the rarity level (1 common, 5 very rare).</summary>
public sealed class RarityMeter : Control
{
    /// <summary>Level.</summary>
    public static readonly StyledProperty<int> LevelProperty = AvaloniaProperty.Register<RarityMeter, int>(nameof(Level));

    private const double CellWidth = 5;
    private const double CellHeight = 10;
    private const double Gap = 2;

    static RarityMeter()
    {
        AffectsRender<RarityMeter>(LevelProperty);
    }

    /// <summary>0 (nothing drawn) to <see cref="Rarity.MaxLevel"/>.</summary>
    public int Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) =>
        new(Rarity.MaxLevel * CellWidth + (Rarity.MaxLevel - 1) * Gap, CellHeight);

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        if (Level <= 0) return;
        var on = MapGeometry.Brush("CautionBrush") ?? Brushes.DarkOrange;
        var off = MapGeometry.Brush("ControlBorderBrush") ?? Brushes.Gray;
        var top = (Bounds.Height - CellHeight) / 2;
        for (var i = 0; i < Rarity.MaxLevel; i++)
            ctx.FillRectangle(i < Level ? on : off, new Rect(i * (CellWidth + Gap), top, CellWidth, CellHeight), 1);
    }
}
