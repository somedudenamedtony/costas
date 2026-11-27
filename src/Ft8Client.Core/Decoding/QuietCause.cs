// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>Why the Operate screen has had no decodes, as <see cref="WhyQuiet"/> reports it, in the order it is checked.</summary>
public enum QuietCause
{
    /// <summary>The sound device delivers no samples.</summary>
    NoAudio,

    /// <summary>Samples arrive but are all zero.</summary>
    Silence,

    /// <summary>The receive audio is below the good band.</summary>
    TooQuiet,

    /// <summary>The receive audio reaches full scale.</summary>
    Clipping,

    /// <summary>The radio's dial is away from the usual frequency for the band and mode.</summary>
    DialOff,

    /// <summary>The radio reports a mode other than USB or USB data.</summary>
    WrongMode,

    /// <summary>The radio reports a receive filter narrower than <see cref="WhyQuiet.MinPassbandHz"/>.</summary>
    NarrowFilter,

    /// <summary>The decoder (jt9) is not running, or failing (as it does while it restarts).</summary>
    Decoder,

    /// <summary>The clock is off by more than the clock limit.</summary>
    Clock,

    /// <summary>Nothing Costas can see is wrong: the band may simply be quiet.</summary>
    NothingFound,
}
