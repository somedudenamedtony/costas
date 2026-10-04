// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Tests;

public class StartupPlanTests
{
    private const string Samples = "samples";

    private static AppSettings Settings(string rigMode = RigModes.None, int? model = null, bool dev = false, bool replay = false, bool partner = false)
    {
        var s = new AppSettings();
        s.Profile.Rig.Mode = rigMode;
        s.Profile.Rig.Model = model;
        s.Developer.Enabled = dev;
        s.Developer.ReplaySamples = replay;
        s.Developer.SimulatedPartner = partner;
        return s;
    }

    [Fact]
    public void Decide_NoRadioNoFlags_NoReplayAndNoRadio()
    {
        var p = StartupPlan.Decide(new CommandLine(), Settings(), Samples);
        p.Replaying.Should().BeFalse("the installed app shows no made-up data");
        p.NoRadio.Should().BeTrue();
        p.SimulatedRig.Should().BeFalse();
        p.Partner.Should().BeFalse();
    }

    [Fact]
    public void Decide_ReplayOptionsWithoutDeveloperMode_Ignored()
    {
        var p = StartupPlan.Decide(new CommandLine(), Settings(dev: false, replay: true, partner: true), Samples);
        p.Replaying.Should().BeFalse();
        p.Partner.Should().BeFalse();
    }

    [Fact]
    public void Decide_DeveloperReplayWithNoRadio_ReplaysWithSimulatedRig()
    {
        var p = StartupPlan.Decide(new CommandLine(), Settings(dev: true, replay: true, partner: true), Samples);
        p.ReplayFolder.Should().Be(Samples);
        p.SimulatedRig.Should().BeTrue();
        p.NoRadio.Should().BeFalse();
        p.Partner.Should().BeTrue();
    }

    [Fact]
    public void Decide_DeveloperReplayWithHamlibDummy_KeepsConfiguredRigAndMutes()
    {
        var s = Settings(RigModes.Hamlib, 1, dev: true, replay: true);
        var p = StartupPlan.Decide(new CommandLine(), s, Samples);
        p.Replaying.Should().BeTrue();
        p.SimulatedRig.Should().BeFalse("the developer is testing against Hamlib's test radio");
        p.MutesTransmitAudio(s.Profile.Rig).Should().BeTrue();
    }

    [Fact]
    public void Decide_CommandLineSimulate_SimulatedRigEvenWithRadioConfigured()
    {
        var p = StartupPlan.Decide(new CommandLine { SimulateFolder = "x", SimulatePartner = true }, Settings(RigModes.Hamlib, 1049), Samples);
        p.ReplayFolder.Should().Be("x");
        p.SimulatedRig.Should().BeTrue();
        p.Partner.Should().BeTrue();
    }

    [Theory]
    [InlineData(RigModes.Hamlib, 1, true)]
    [InlineData(RigModes.Hamlib, 6, true)]
    [InlineData(RigModes.Hamlib, 1049, false)]
    [InlineData(RigModes.Vox, null, false)]
    public void MutesTransmitAudio_LiveAudio_OnlyForHamlibTestRadios(string mode, int? model, bool muted)
    {
        var s = Settings(mode, model);
        StartupPlan.Decide(new CommandLine(), s, Samples).MutesTransmitAudio(s.Profile.Rig).Should().Be(muted);
    }
}
