// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Data.Settings;
using Ft8Client.Rig;

namespace Ft8Client.App.Engine;

/// <summary>
/// What the app runs against, decided once at start-up. Replayed recordings only with <c>--simulate</c> or developer
/// mode; with no radio set up the app receives nothing and refuses to transmit, rather than showing made-up data.
/// </summary>
/// <param name="ReplayFolder">Recordings to replay instead of the sound card, or null for live audio.</param>
/// <param name="Partner">Add the simulated station that answers my calls (only while replaying).</param>
/// <param name="SimulatedRig">Use the in-process simulated radio.</param>
/// <param name="NoRadio">No radio is set up: the radio indicator says so and transmitting is refused.</param>
public sealed record StartupPlan(string? ReplayFolder, bool Partner, bool SimulatedRig, bool NoRadio)
{
    /// <summary>True when replaying recordings (decodes are off-air; no spots, journal or WAV files).</summary>
    public bool Replaying => ReplayFolder is not null;

    /// <summary>Decides from the command line and settings. <paramref name="samplesFolder"/> is the bundled samples.</summary>
    public static StartupPlan Decide(CommandLine args, AppSettings s, string samplesFolder)
    {
        var dev = s.Developer;
        var rig = s.Profile.Rig;
        var folder = args.SimulateFolder ?? (dev.Enabled && dev.ReplaySamples ? samplesFolder : null);
        var replaying = folder is not null;
        var partner = replaying && (args.SimulatePartner || (dev.Enabled && dev.SimulatedPartner));
        // --simulate keeps the in-process radio even when one is configured; developer replay uses the configured radio.
        var simulatedRig = replaying && (args.SimulateFolder is not null || rig.Mode == RigModes.None);
        var noRadio = !simulatedRig && rig.Mode == RigModes.None;
        return new StartupPlan(folder, partner, simulatedRig, noRadio);
    }

    /// <summary>
    /// Transmit audio never reaches the sound card while replaying or with a Hamlib test radio: nothing real is keyed,
    /// and a radio with VOX on must not be keyed by the test either.
    /// </summary>
    public bool MutesTransmitAudio(RigSettings rig) =>
        Replaying || (rig.Mode == RigModes.Hamlib && HamlibModels.IsTestModel(rig.Model));
}
