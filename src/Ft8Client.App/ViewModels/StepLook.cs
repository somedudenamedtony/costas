// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>How a conversation row looks.</summary>
public enum StepLook
{
    /// <summary>Sent or received: plain row, primary text.</summary>
    Done,

    /// <summary>On the air now: critical fill, white text.</summary>
    Transmitting,

    /// <summary>The current step when not on the air: accent tint and border.</summary>
    Waiting,

    /// <summary>Not reached yet: secondary text.</summary>
    Upcoming,
}
