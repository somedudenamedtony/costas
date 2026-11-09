// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contacts;

namespace Ft8Client.App.Engine;

/// <summary>Whether Tune is going and whether it can be pressed, the same for the desk's Tune button and the phone's.</summary>
public static class TuneState
{
    /// <summary>A Tune tone on the air, or the radio's own tuner started by Tune and not yet past the Tune limit.</summary>
    public static bool Tuning(SessionSnapshot s, DateTime nowUtc) => s.TransmittingMessage == Session.TuneMessage || s.RigTuningUntilUtc > nowUtc;

    /// <summary>True when Tune can be pressed: nothing else is being sent, or a tune is going and can be stopped.</summary>
    public static bool CanTune(SessionSnapshot s, DateTime nowUtc) =>
        Tuning(s, nowUtc) || (!s.Transmitting && !s.CallingCq && s.Contact is not { Outcome: ContactOutcome.InProgress });
}
