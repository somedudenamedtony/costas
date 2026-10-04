// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>How a conversation-strip box looks.</summary>
public enum StepLook
{
    /// <summary>Accent tint, solid border.</summary>
    Done,

    /// <summary>Critical fill, white text.</summary>
    Transmitting,

    /// <summary>Accent border, no fill.</summary>
    Waiting,

    /// <summary>Dashed border, secondary text.</summary>
    Upcoming,
}

/// <summary>One box of the conversation strip.</summary>
/// <param name="Index">Step index, for jumping.</param>
/// <param name="Mine">Mine (raised) or theirs (lowered).</param>
/// <param name="Header">"You · sent 02:54:00", "ZL2RPA · expected 02:54:45".</param>
/// <param name="Message">Literal message.</param>
/// <param name="Look">Look.</param>
/// <param name="CanJump">True for my upcoming boxes (click to jump).</param>
public sealed record StepViewModel(int Index, bool Mine, string Header, string Message, StepLook Look, bool CanJump)
{
    /// <summary>Vertical offset: mine raised 12 px, theirs lowered 12 px.</summary>
    public double Offset => Mine ? -12 : 12;
}
