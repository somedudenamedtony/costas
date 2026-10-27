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

namespace Ft8Client.App.Views;

/// <summary>Power out, ALC and SWR meters on an instrument skin's faceplate, in that skin's colours.</summary>
public partial class TxFaceMeters : UserControl
{
    public static readonly StyledProperty<IBrush> LabelBrushProperty = AvaloniaProperty.Register<TxFaceMeters, IBrush>(nameof(LabelBrush), Brushes.Gray);
    public static readonly StyledProperty<IBrush> ValueBrushProperty = AvaloniaProperty.Register<TxFaceMeters, IBrush>(nameof(ValueBrush), Brushes.White);
    public static readonly StyledProperty<IBrush> MeterBrushProperty = AvaloniaProperty.Register<TxFaceMeters, IBrush>(nameof(MeterBrush), Brushes.Cyan);
    public static readonly StyledProperty<IBrush> CautionBrushProperty = AvaloniaProperty.Register<TxFaceMeters, IBrush>(nameof(CautionBrush), Brushes.Orange);
    public static readonly StyledProperty<IBrush> TrackBrushProperty = AvaloniaProperty.Register<TxFaceMeters, IBrush>(nameof(TrackBrush), Brushes.DimGray);

    /// <summary>Creates the control.</summary>
    public TxFaceMeters() => InitializeComponent();

    public IBrush LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush ValueBrush { get => GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
    public IBrush MeterBrush { get => GetValue(MeterBrushProperty); set => SetValue(MeterBrushProperty, value); }
    public IBrush CautionBrush { get => GetValue(CautionBrushProperty); set => SetValue(CautionBrushProperty, value); }
    public IBrush TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
}
