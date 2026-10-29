// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Media;

namespace Ft8Client.App.Services;

/// <summary>Surface and accent tokens for one light or dark skin palette.</summary>
public sealed record SkinPalette(
    string Window, string Card, string Border, string ControlBorder,
    string Text, string Secondary, string Accent, string AccentText, string Selection, string Divider)
{
    /// <summary>Preview swatches use the same colors as the applied skin.</summary>
    public IBrush WindowBrush => Brush.Parse(Window);
    public IBrush CardBrush => Brush.Parse(Card);
    public IBrush TextBrush => Brush.Parse(Text);
    public IBrush SecondaryBrush => Brush.Parse(Secondary);
    public IBrush AccentBrush => Brush.Parse(Accent);
    public IBrush SelectionBrush => Brush.Parse(Selection);
}
