// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using CommunityToolkit.Mvvm.ComponentModel;

namespace Ft8Client.App.ViewModels;

/// <summary>One PSK and RTTY macro in the Settings editor.</summary>
public sealed partial class MacroEditViewModel : ObservableObject
{
    /// <summary>Creates a row for function key <paramref name="key"/>.</summary>
    public MacroEditViewModel(int key, string label, string text)
    {
        Key = key;
        Label = label;
        Text = text;
    }

    /// <summary>The function key, 1 for F1.</summary>
    public int Key { get; }

    /// <summary>"F3".</summary>
    public string KeyName => $"F{Key}";

    /// <summary>The button's name.</summary>
    [ObservableProperty]
    public partial string Label { get; set; }

    /// <summary>The text, with fields such as {CALL}.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }
}
