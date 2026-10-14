// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.App.ViewModels;

/// <summary>One over in the conversation box: time, who and text, mine dimmed.</summary>
/// <param name="Time">UTC, HH:mm:ss.</param>
/// <param name="Who">The sender's call.</param>
/// <param name="Text">What was sent.</param>
/// <param name="Mine">True for what I sent.</param>
public sealed record ConversationRowViewModel(string Time, string Who, string Text, bool Mine)
{
    /// <summary>The words of the text, each one clickable to capture as part of the exchange.</summary>
    public IReadOnlyList<string> Words { get; } = [.. Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
}
