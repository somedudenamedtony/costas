// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.UpdateSplash;

/// <summary>
/// Moves the splash through its phases from what it can see: whether the old Costas has exited, and whether a Costas
/// has started or put its window up. Each phase has a time limit, so the splash never stays up for good when something
/// goes wrong (Velopack failed and started nothing, or the new Costas stopped at start).
/// </summary>
public sealed class SplashProgress
{
    /// <summary>The old Costas normally closes in a few seconds; it is given this long.</summary>
    public static readonly TimeSpan ClosingLimit = TimeSpan.FromMinutes(2);

    /// <summary>Applying an update normally takes a few seconds; a slow disk or a virus scanner can take much longer.</summary>
    public static readonly TimeSpan InstallingLimit = TimeSpan.FromMinutes(3);

    /// <summary>From Costas starting to its window being on screen.</summary>
    public static readonly TimeSpan OpeningLimit = TimeSpan.FromMinutes(2);

    /// <summary>How long the splash says the update did not work before it closes.</summary>
    public static readonly TimeSpan FailedShown = TimeSpan.FromSeconds(45);

    /// <summary>After this long installing, the status says it is still going.</summary>
    public static readonly TimeSpan SlowInstall = TimeSpan.FromSeconds(20);

    private TimeSpan _phaseStart;
    private TimeSpan _now;

    /// <summary>The current phase.</summary>
    public SplashPhase Phase { get; private set; } = SplashPhase.Closing;

    /// <summary>
    /// Updates the phase at <paramref name="now"/> (time since the splash started) and returns it. A phase is never
    /// gone back to. Running out of time while installing or opening is a failed update (<see cref="SplashPhase.Failed"/>,
    /// shown for <see cref="FailedShown"/>); a Costas that shows its window after all still ends the splash at once.
    /// </summary>
    public SplashPhase Update(TimeSpan now, bool oldExited, bool started, bool ready)
    {
        _now = now;
        var next = Phase;
        if (ready) next = SplashPhase.Done;
        else if (started && Phase < SplashPhase.Opening) next = SplashPhase.Opening;
        else if (oldExited && Phase < SplashPhase.Installing) next = SplashPhase.Installing;
        if (next != Phase)
        {
            Phase = next;
            _phaseStart = now;
        }
        var limit = Phase switch
        {
            SplashPhase.Closing => ClosingLimit,
            SplashPhase.Installing => InstallingLimit,
            SplashPhase.Opening => OpeningLimit,
            SplashPhase.Failed => FailedShown,
            _ => TimeSpan.MaxValue,
        };
        if (Phase != SplashPhase.Done && now - _phaseStart >= limit)
        {
            // The old Costas never closing is not the new one failing: nothing was installed, so there is nothing to say.
            Phase = Phase is SplashPhase.Installing or SplashPhase.Opening ? SplashPhase.Failed : SplashPhase.Done;
            _phaseStart = now;
        }
        return Phase;
    }

    /// <summary>The title for the current phase.</summary>
    public string Title => Phase == SplashPhase.Failed ? "Costas did not open" : "Updating Costas";

    /// <summary>The status line under the title for the current phase.</summary>
    public string Status(string version)
    {
        var v = version.Length > 0 ? $" {version}" : string.Empty;
        return Phase switch
        {
            SplashPhase.Closing => "Closing Costas…",
            SplashPhase.Installing when _now - _phaseStart >= SlowInstall => $"Still installing{(version.Length > 0 ? $" version {version}" : string.Empty)}; this can take a minute…",
            SplashPhase.Installing => version.Length > 0 ? $"Installing version {version}…" : "Installing the update…",
            SplashPhase.Failed => "Windows may have blocked the update. Opening the help page…",
            _ => $"Opening Costas{v}…",
        };
    }
}
