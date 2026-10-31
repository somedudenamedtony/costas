// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Ft8Client.App.ViewModels;

/// <summary>One Show chip on Raw decodes, with how many lines it would show.</summary>
public sealed partial class ShowOptionViewModel(string choice, string label) : ObservableObject
{
    /// <summary>The show choice this chip picks.</summary>
    public string Choice { get; } = choice;

    /// <summary>Chip text.</summary>
    public string Label { get; } = label;

    /// <summary>Lines this choice would show.</summary>
    [ObservableProperty]
    public partial string Count { get; private set; } = "0";

    /// <summary>True for the chosen chip.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; private set; }

    /// <summary>Updates the count and selection.</summary>
    public void Update(int count, bool selected)
    {
        Count = count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        IsSelected = selected;
    }
}
