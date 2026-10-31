// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.App.ViewModels;

/// <summary>A function-key macro button under the conversation box.</summary>
/// <param name="Key">The function key, 1 for F1.</param>
/// <param name="Label">The macro's name.</param>
/// <param name="Text">The macro's text as it would go out now, for the tooltip.</param>
/// <param name="IsNext">True for the next step's macro (Enter sends it), shown in the accent colour.</param>
public sealed record MacroButtonViewModel(int Key, string Label, string Text, bool IsNext)
{
    /// <summary>"F3 Exchange".</summary>
    public string Caption => $"F{Key} {Label}";
}
