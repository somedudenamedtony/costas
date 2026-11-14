// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.UpdateSplash;

/// <summary>
/// What the update splash and Costas agree on: the program's name and the named events through which they hand over.
/// This file is also compiled into the app (linked in Ft8Client.App.csproj), so the two cannot drift apart.
/// </summary>
/// <remarks>
/// The old Costas creates <see cref="ShownEvent"/> and waits for the splash to set it before it closes, so its window
/// never leaves the screen empty. The splash creates <see cref="StartedEvent"/> and <see cref="ReadyEvent"/>; the
/// next Costas to start (the new version, or the old one again if the update failed) sets the first as it starts and
/// the second once its window is on screen, and the splash closes. The names are per Windows session (<c>Local\</c>).
/// </remarks>
public static class SplashProtocol
{
    /// <summary>The splash program, published beside Costas.exe.</summary>
    public const string ExeName = "CostasUpdateSplash.exe";

    /// <summary>Set by the splash once its window is showing.</summary>
    public const string ShownEvent = @"Local\Costas.UpdateSplash.Shown";

    /// <summary>Set by Costas as it starts.</summary>
    public const string StartedEvent = @"Local\Costas.UpdateSplash.Started";

    /// <summary>Set by Costas once its main window is on screen.</summary>
    public const string ReadyEvent = @"Local\Costas.UpdateSplash.Ready";
}
