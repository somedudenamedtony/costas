// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.ViewModels;

/// <summary>One contact in the Contest tab's log.</summary>
/// <param name="Time">UTC time, HH:mm.</param>
/// <param name="Band">Band.</param>
/// <param name="Call">Their call.</param>
/// <param name="Sent">My exchange.</param>
/// <param name="Received">Their exchange.</param>
/// <param name="Tag">"New mult", "Dupe", or empty.</param>
/// <param name="Dupe">True when the contact does not count.</param>
public sealed record ContestLogRowViewModel(string Time, string Band, string Call, string Sent, string Received, string Tag, bool Dupe)
{
    /// <summary>True when there is a tag.</summary>
    public bool HasTag => Tag.Length > 0;
}
