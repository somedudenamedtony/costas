// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One box of the conversation strip.</summary>
/// <param name="Index">Step index, for jumping.</param>
/// <param name="Mine">Mine (raised) or theirs (lowered).</param>
/// <param name="Who">"You" or the DX call.</param>
/// <param name="Time">UTC time sent, received or expected ("20:29:31"); blank when not known yet.</param>
/// <param name="Message">Literal message.</param>
/// <param name="Status">"Sent", "Sending · 9 s left", "Waiting for reply", "Received ×2", "Next".</param>
/// <param name="Look">Look.</param>
/// <param name="CanJump">True for my upcoming boxes (click to jump).</param>
public sealed record StepViewModel(int Index, bool Mine, string Who, string Time, string Message, string Status, StepLook Look, bool CanJump)
{
    /// <summary>The progress marker: a tick once done, otherwise the step number from 1.</summary>
    public string Marker => Look == StepLook.Done ? "✓" : (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True for the first step (no track to its left).</summary>
    public bool First { get; init; }

    /// <summary>True for the last step (no track to its right).</summary>
    public bool Last { get; init; }

    /// <summary>The track into this step is filled: this step has been reached.</summary>
    public bool Reached => Look != StepLook.Upcoming;

    /// <summary>The track out of this step is filled: the next step has been reached.</summary>
    public bool NextReached { get; init; }

    /// <summary>Vertical offset: mine raised 12 px, theirs lowered 12 px.</summary>
    public double Offset => Mine ? -12 : 12;
}
