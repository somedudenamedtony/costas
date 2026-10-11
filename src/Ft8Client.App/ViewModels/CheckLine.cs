// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One line of a connection test: passed, failed, or a note.</summary>
/// <param name="Ok">True passed, false failed, null a note.</param>
/// <param name="Text">What was checked, and what to fix when it failed.</param>
public sealed record CheckLine(bool? Ok, string Text)
{
    /// <summary>✓, ✗ or a bullet.</summary>
    public string Mark => Ok switch
    {
        true => "✓",
        false => "✗",
        null => "•",
    };
}
