// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Dsp;

/// <summary>How the receive audio level looks, for Setup's input meter.</summary>
public enum InputLevelState
{
    /// <summary>Nothing measured yet.</summary>
    Waiting,

    /// <summary>The device delivered no samples for a while: unplugged, stopped, or held by another program.</summary>
    NoAudio,

    /// <summary>Samples arrive but are all exactly zero: usually the wrong device, or the radio's USB audio is off.</summary>
    Silence,

    /// <summary>Below the good band.</summary>
    TooQuiet,

    /// <summary>In the good band.</summary>
    Good,

    /// <summary>Above the good band.</summary>
    TooLoud,

    /// <summary>Samples reach full scale: the audio is being cut off.</summary>
    Clipping,
}
