// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One choice in the CQ modifier picker beside Call CQ: empty for a plain CQ.</summary>
/// <param name="Modifier">The modifier as sent, such as <c>DX</c>; empty for a plain CQ.</param>
public sealed record CqModifierChoice(string Modifier)
{
    /// <summary>The modifiers offered without typing one in Settings.</summary>
    public static readonly IReadOnlyList<string> Common = ["", "DX", "NA", "SA", "EU", "AS", "AF", "OC", "POTA"];

    /// <summary>"CQ" or "CQ DX".</summary>
    public string Label => Modifier.Length == 0 ? "CQ" : "CQ " + Modifier;
}
