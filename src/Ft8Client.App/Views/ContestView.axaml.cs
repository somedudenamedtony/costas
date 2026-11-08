// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Avalonia.Controls;

namespace Ft8Client.App.Views;

/// <summary>The Contest view: score, multipliers and the contest log while a contest is picked.</summary>
public partial class ContestView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ContestView() => InitializeComponent();
}
