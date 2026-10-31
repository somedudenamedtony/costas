// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>What is wrong with the receive audio, as the Operate screen reports it.</summary>
public enum ReceiveLevelProblem
{
    /// <summary>Nothing wrong.</summary>
    None,

    /// <summary>The input device delivers no samples: unplugged, asleep, or held by another program.</summary>
    NoAudio,

    /// <summary>Samples arrive but are all zero: the radio's USB audio is off or muted, or the wrong device.</summary>
    Silence,

    /// <summary>The audio reaches full scale: it is too loud and being cut off.</summary>
    Clipping,
}
