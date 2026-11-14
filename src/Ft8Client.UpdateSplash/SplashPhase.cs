// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.UpdateSplash;

/// <summary>Where the update has got to, as the splash shows it.</summary>
public enum SplashPhase
{
    /// <summary>The old Costas is still closing (stopping jt9 and rigctld, saving the log).</summary>
    Closing,

    /// <summary>The old Costas has gone; Velopack is putting the new files in place.</summary>
    Installing,

    /// <summary>A Costas has started and its window is on the way.</summary>
    Opening,

    /// <summary>
    /// Nothing started after the files were swapped, or a Costas started and never showed its window: the update did
    /// not work. The splash says so and opens the help page before it closes.
    /// </summary>
    Failed,

    /// <summary>Costas is on screen, or the splash gave up waiting: the splash closes.</summary>
    Done,
}
