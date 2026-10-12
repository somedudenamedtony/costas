// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Bands;

namespace Ft8Client.App.ViewModels;

/// <summary>A dial frequency the operator saved, in the frequency menu.</summary>
/// <param name="Hz">Dial frequency in hertz.</param>
public sealed record SavedDialViewModel(long Hz)
{
    /// <summary>"17 m · 18.095 MHz".</summary>
    public string Text => (BandPlan.BandOf(Hz) is { } b ? BandPlan.Display(b) + " · " : string.Empty) + DialText.Mhz(Hz) + " MHz";
}
