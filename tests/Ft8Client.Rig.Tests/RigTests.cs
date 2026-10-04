// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Processes;
using Ft8Client.Core.Time;
using Ft8Client.Rig;

namespace Ft8Client.Rig.Tests;

public class RigTests
{
    [Fact]
    public void Reply_ExtendedProtocol_Parses()
    {
        var r = RigctldReply.Parse(["get_mode:", "Mode: PKTUSB", "Passband: 3000", "RPRT 0"]);
        r.Ok.Should().BeTrue();
        r["Mode"].Should().Be("PKTUSB");
        r["Passband"].Should().Be("3000");
        RigctldReply.Parse(["set_freq: nonsense", "RPRT -1"]).Code.Should().Be(-1);
        RigctldReply.Parse(["get_split_vfo:", "Split: 0", "TX VFO: VFOA", "RPRT 0"])["TX VFO"].Should().Be("VFOA");
    }

    [Fact]
    public void Options_Arguments_BindLocalhostAndMapPtt()
    {
        var a = new RigctldOptions("rigctld", 3073, "COM4", 115200, "rts", "COM5").Arguments(4532);
        string.Join(' ', a).Should().Be("-m 3073 -r COM4 -s 115200 -P RTS -p COM5 -T 127.0.0.1 -t 4532");
        string.Join(' ', new RigctldOptions("rigctld", 1, null, null).Arguments(1)).Should().Be("-m 1 -P RIG -T 127.0.0.1 -t 1");
    }

    [Fact]
    public void Models_ParseRigctlList()
    {
        const string text = """
             Rig #  Mfg                    Model                   Version         Status      Macro
                 1  Hamlib                 Dummy                   20221128.0      Stable      RIG_MODEL_DUMMY
                 5  TRXManager             TRXManager 5.7.630+     20210613.0      Stable      RIG_MODEL_TRXMANAGER_RIG
              3073  Icom                   IC-7300                 20230109.17     Stable      RIG_MODEL_IC7300
            """;
        var m = HamlibModels.Parse(text);
        m.Should().HaveCount(3);
        m[2].Should().Be(new HamlibModel(3073, "Icom", "IC-7300", "Stable"));
        m[1].Model.Should().Be("TRXManager 5.7.630+");
    }

    [Fact]
    public async Task SimulatedRig_LogsPtt_AndFaultStillReleases()
    {
        var clock = new ManualClock(new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc));
        var rig = new SimulatedRig(clock);
        await rig.SetPttAsync(true, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(13));
        rig.FailNext = "cable unplugged";
        var faults = new List<RigFault>();
        rig.Faulted += (_, f) => faults.Add(f);
        await rig.Invoking(r => r.SetFrequencyAsync(7_074_000, default)).Should().ThrowAsync<RigException>();
        await rig.SetPttAsync(false, TestContext.Current.CancellationToken);
        rig.Ptt.Should().BeFalse();
        faults.Should().ContainSingle();
        rig.PttLog.Select(p => p.On).Should().Equal(true, false);
    }

    [Fact]
    public async Task Rigctld_DummyRig_SetsAndReadsBackAndRecoversFromCrash()
    {
        var exe = HamlibModels.Find("rigctld", null);
        Assert.SkipWhen(exe is null, "rigctld is not installed.");
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await RigctldRig.StartAsync(new RigctldOptions(exe!, 1, null, null), NullProcessGuard.Instance, SystemClock.Instance, ct);

        await rig.SetFrequencyAsync(14_074_000, ct);
        await rig.SetModeAsync(RigMode.PktUsb, ct);
        await rig.SetPttAsync(true, ct);
        var s = await rig.GetStateAsync(ct);
        s.Should().Be(new RigState(14_074_000, "PKTUSB", true, false));
        await rig.SetPttAsync(false, ct);
        (await rig.GetStateAsync(ct)).Ptt.Should().BeFalse();
        await rig.SetSplitAsync(true, 14_075_000, ct);
        (await rig.GetStateAsync(ct)).Split.Should().BeTrue();

        // Kill rigctld behind the client's back: the next command faults, then the supervisor restarts it.
        var faults = new List<RigFault>();
        rig.Faulted += (_, f) => faults.Add(f);
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("rigctld"))
        {
            p.Kill();
            await p.WaitForExitAsync(ct);
        }
        // The exit is reported with no command pending, and commands fail (rather than quietly restarting) until it is back.
        var faultDeadline = DateTime.UtcNow.AddSeconds(5);
        while (faults.Count == 0 && DateTime.UtcNow < faultDeadline) await Task.Delay(20, ct);
        faults.Should().NotBeEmpty();
        await rig.Invoking(r => r.SetFrequencyAsync(7_074_000, ct)).Should().ThrowAsync<RigException>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await rig.SetFrequencyAsync(7_074_000, ct);
                break;
            }
            catch (RigException)
            {
                await Task.Delay(250, ct);
            }
        }
        (await rig.GetStateAsync(ct)).FrequencyHz.Should().Be(7_074_000);
        rig.Restarts.Should().BeGreaterThan(0);
    }

    // Hamlib's real FT-710 backend (model 1049) against a CAT emulator on a pseudo-terminal: checks the commands the
    // radio would receive. Linux only (needs python3 and rigctld); kept in this class so the crash test above, which
    // kills every rigctld, never runs at the same time.
    [Fact]
    public async Task Rigctld_Ft710Emulator_SendsDataUsbAndCatPttWithoutTouchingFilter()
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
                await rig.SetFrequencyAsync(14_074_000, ct);
                await rig.SetModeAsync(RigMode.PktUsb, ct);
                await rig.SetPttAsync(true, ct);
                (await rig.GetStateAsync(ct)).Should().Match<RigState>(s => s.FrequencyHz == 14_074_000 && s.Mode == "PKTUSB" && s.Ptt);
                await rig.SetPttAsync(false, ct);
                (await rig.GetStateAsync(ct)).Ptt.Should().BeFalse();
            }
            var sent = File.ReadAllLines(log);
            sent.Should().Contain("FA014074000;", "VFO A set to the dial frequency");
            sent.Should().Contain("MD0C;", "DATA-U is the FT-710's data mode");
            sent.Should().ContainInOrder("TX1;", "TX0;");
            sent.Should().NotContain(l => l.StartsWith("SH0", StringComparison.Ordinal) && l.Length > 4, "the operator's receive filter is left alone");
        }
        finally
        {
            emu.Kill();
            File.Delete(log);
        }
    }

    [Fact]
    public void ForPicker_RigctlList_SortedAndTestRadiosOnlyInDeveloperMode()
    {
        var all = HamlibModels.Parse("""
             Rig #  Mfg                    Model                   Version         Status      Macro
                 1  Hamlib                 Dummy                   20221128.0      Stable      RIG_MODEL_DUMMY
                 2  Hamlib                 NET rigctl              20221111.0      Stable      RIG_MODEL_NETRIGCTL
                 6  Hamlib                 Dummy No VFO            20221128.0      Stable      RIG_MODEL_DUMMY_NOVFO
              3073  Icom                   IC-7300                 20230109.6      Stable      RIG_MODEL_IC7300
              1049  Yaesu                  FT-710                  20230328.3      Stable      RIG_MODEL_FT710
              1035  Yaesu                  FT-991                  20230328.15     Stable      RIG_MODEL_FT991
            """);
        HamlibModels.ForPicker(all, false).Select(m => m.Display).Should().Equal("Hamlib NET rigctl", "Icom IC-7300", "Yaesu FT-710", "Yaesu FT-991");
        HamlibModels.ForPicker(all, true).Select(m => m.Number).Should().Contain([1, 6]);
        all.Single(m => m.Number == 1049).Display.Should().Be("Yaesu FT-710");
    }
}
