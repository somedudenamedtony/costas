// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Processes;
using Ft8Client.Core.Time;

namespace Ft8Client.Rig.Tests;

public class RigControlTests
{
    // Lines from Hamlib 4.5.5's dump_caps for the FT-710 (rigctl -m 1049 -u), as rigctld sends them.
    private static readonly string[] Ft710Caps =
    [
        "dump_caps:",
        "Preamp: 10dB 20dB",
        "Attenuator: 6dB 12dB 18dB",
        "AGC levels: 0=OFF 2=FAST 5=MEDIUM 3=SLOW 6=AUTO",
        "Get functions: NB COMP VOX TONE TSQL FBKIN ANF NR APF MON MN LOCK RIT TUNER XIT ",
        "Set functions: NB COMP VOX TONE TSQL FBKIN ANF NR APF MON MN LOCK RIT TUNER XIT ",
        "Get level: PREAMP(10..20/10) ATT(12..12/0) AF(0..1/0.00392157) RF(0..1/0.00392157) SQL(0..1/0.01) AGC(0..0/0) SWR(0..5/0.00392157) ALC(0..1/0.01) RFPOWER_METER_WATTS(0..100/0.00392157) ",
        "Set level: PREAMP(10..20/10) ATT(12..12/0) AF(0..1/0.00392157) RF(0..1/0.00392157) SQL(0..1/0.01) AGC(0..0/0) ",
        "RPRT 0",
    ];

    [Fact]
    public void Parse_Ft710DumpCaps_OffersEverySetting()
    {
        var reply = RigctldReply.Parse(Ft710Caps);
        var caps = RigCapabilities.Parse(k => reply[k]);

        Enum.GetValues<RigControl>().Should().OnlyContain(c => caps.Supports(c));
        caps.Options(RigControl.Preamp).Should().Equal(0, 10, 20);
        caps.Options(RigControl.Attenuator).Should().Equal(0, 6, 12, 18);
        caps.Options(RigControl.Agc).Should().Equal(0, 2, 5, 3, 6);
    }

    [Fact]
    public void Parse_ReadOnlyLevelOrNoSteps_NotOffered()
    {
        var caps = RigCapabilities.Parse(k => k switch
        {
            "Get level" => "AF(0..1/0.01) RF(0..1/0.01) PREAMP(0..0/0) AGC(0..0/0)",
            "Set level" => "RF(0..1/0.01) PREAMP(0..0/0) AGC(0..0/0)",
            "Get functions" => "NB TUNER",
            "Set functions" => "NB",
            "Preamp" => "None",
            "AGC levels" => "-1=NONE",
            _ => null,
        });

        caps.Supports(RigControl.AfGain).Should().BeFalse("AF is read-only here");
        caps.Supports(RigControl.RfGain).Should().BeTrue();
        caps.Supports(RigControl.Preamp).Should().BeFalse("the radio lists no preamp steps");
        caps.Supports(RigControl.Agc).Should().BeFalse("the radio lists no AGC speeds");
        caps.Supports(RigControl.NoiseBlanker).Should().BeTrue();
        caps.Supports(RigControl.Tuner).Should().BeFalse("the tuner can be read but not set");
        caps.Supports(RigControl.NoiseReduction).Should().BeFalse();
    }

    [Fact]
    public void Parse_IcomNumberedPreamps_KeepsHamlibsNumbers()
    {
        var caps = RigCapabilities.Parse(k => k == "Preamp" ? "1dB 2dB" : null);
        caps.Options(RigControl.Preamp).Should().Equal(0, 1, 2);
    }

    // Through Hamlib's FT-710 backend: the settings are read with AG0, RG0, PA0, RA0, GT0, NB0, NR0 and AC, and set with
    // the same commands; nothing keys the radio or starts a tuning cycle (AC003). Ubuntu's Hamlib 4.5.5 sends RF gain
    // without its terminator for the FT-710 (fixed in 4.6), so RF gain is not checked here.
    [Fact]
    public async Task ReadAndSetControls_Ft710Emulator_SendsNewcatCommandsWithoutKeying()
    {
        var exe = HamlibModels.Find("rigctld", null);
        var python = HamlibModels.Find("python3", null);
        Assert.SkipWhen(exe is null || python is null || !OperatingSystem.IsLinux(), "Needs Linux with rigctld and python3.");
        var ct = TestContext.Current.CancellationToken;
        var log = Path.Combine(Path.GetTempPath(), $"ft710-{Guid.NewGuid():N}.log");
        using var emu = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(python!,
            [Path.Combine(AppContext.BaseDirectory, "Emulators", "ft710.py"), log]) { RedirectStandardOutput = true })!;
        try
        {
            var pty = (await emu.StandardOutput.ReadLineAsync(ct))!.Trim();
            await using (var rig = await RigctldRig.StartAsync(new RigctldOptions(exe!, 1049, pty, 38400), NullProcessGuard.Instance, SystemClock.Instance, ct))
            {
                var faults = new List<RigFault>();
                rig.Faulted += (_, f) => faults.Add(f);
                var read = await rig.ReadControlsAsync(ct);
                read.Select(c => (c.Control, c.Value)).Should().Equal(
                    (RigControl.AfGain, 50), (RigControl.RfGain, 100), (RigControl.Preamp, 0), (RigControl.Attenuator, 0),
                    (RigControl.Agc, 6), (RigControl.NoiseBlanker, 0), (RigControl.NoiseReduction, 0), (RigControl.Tuner, 0));
                read.Single(c => c.Control == RigControl.Preamp).Options.Should().Equal(0, 10, 20);

                (await rig.SetControlAsync(RigControl.AfGain, 30, ct)).Should().BeTrue();
                (await rig.SetControlAsync(RigControl.Preamp, 20, ct)).Should().BeTrue();
                (await rig.SetControlAsync(RigControl.Attenuator, 12, ct)).Should().BeTrue();
                (await rig.SetControlAsync(RigControl.Agc, 3, ct)).Should().BeTrue();
                (await rig.SetControlAsync(RigControl.NoiseBlanker, 1, ct)).Should().BeTrue();
                (await rig.SetControlAsync(RigControl.Tuner, 1, ct)).Should().BeTrue();
                (await rig.SetControlAsync(RigControl.Preamp, 15, ct)).Should().BeFalse("15 dB is not one of the radio's steps");

                var after = await rig.ReadControlsAsync(ct);
                after.Single(c => c.Control == RigControl.Preamp).Value.Should().Be(20);
                after.Single(c => c.Control == RigControl.Agc).Value.Should().Be(3);
                after.Single(c => c.Control == RigControl.Tuner).Value.Should().Be(1);
                (await rig.GetStateAsync(ct)).Ptt.Should().BeFalse();
                faults.Should().BeEmpty();
            }
            var sent = File.ReadAllLines(log);
            sent.Should().Contain(["AG0077;", "PA02;", "RA02;", "GT03;", "NB01;", "AC001;"]);
            sent.Should().NotContain("TX1;", "changing a setting never keys the radio");
            sent.Should().NotContain(l => l.StartsWith("AC00", StringComparison.Ordinal) && (l[4] == '2' || l[4] == '3'), "switching the tuner in never starts a tune");
        }
        finally
        {
            emu.Kill();
            File.Delete(log);
        }
    }
}
