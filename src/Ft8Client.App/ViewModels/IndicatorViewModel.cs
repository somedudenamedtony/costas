// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Services;

namespace Ft8Client.App.ViewModels;

/// <summary>A bottom-bar indicator: a dot and a word.</summary>
/// <param name="Label">Radio, Audio, QRZ, PSK Reporter.</param>
/// <param name="Word">State word.</param>
/// <param name="Health">Health (dot colour).</param>
/// <param name="Detail">Tooltip.</param>
public sealed record IndicatorViewModel(string Label, string Word, ServiceHealth Health, string Detail)
{
    /// <summary>Text shown: "Radio ok".</summary>
    public string Text => $"{Label} {Word}";
}
