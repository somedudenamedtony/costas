// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems;

/// <summary>
/// Transmit audio for a keyboard mode, a piece at a time, so text typed while sending joins the over without a break.
/// Each call returns the audio for that piece, continuing the last one's phase. An over is <see cref="Start"/>, any
/// mix of <see cref="Send"/> and <see cref="Idle"/>, then <see cref="Stop"/>.
/// </summary>
public interface IKeyboardModulator
{
    /// <summary>The audio that opens an over.</summary>
    float[] Start();

    /// <summary>One character. Throws for a character <see cref="CanSend"/> refuses.</summary>
    float[] Send(char c);

    /// <summary>A short stretch of the mode's idle, for when nothing is waiting to be sent.</summary>
    float[] Idle();

    /// <summary>The audio that closes an over, ending in silence.</summary>
    float[] Stop();

    /// <summary>True when the mode can send the character exactly (after <see cref="Normalize"/>).</summary>
    bool CanSend(char c);

    /// <summary>The character as it goes out: upper case for RTTY, unchanged for PSK.</summary>
    char Normalize(char c);
}
