// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;
using Ft8Client.Audio.Backends;
using Ft8Client.Data.Import;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class WsjtxSetupImportTests
{
    private static readonly HamlibModel Ic7300 = new(3073, "Icom", "IC-7300", "Stable");
    private static readonly HamlibModel Flrig = new(4, "FLRig", "FLRig", "Stable");
    private static readonly AudioDevice CodecIn = new("in-1", "Microphone (USB Audio CODEC )");
    private static readonly AudioDevice CodecOut = new("out-1", "Speakers (USB Audio CODEC )");

    private static WsjtxSettings Settings(string rig = "Icom IC-7300", string ptt = "cat", string pttPort = "CAT", string soundIn = "Microphone (USB Audio CODEC )",
                                          string soundOut = "Speakers (USB Audio CODEC )", string network = "") =>
        new("K1ABC", "FN42", rig, "COM4", network, 19200, ptt, pttPort, soundIn, soundOut);

    [Fact]
    public void Plan_IcomOnSerial_CopiesEverything()
    {
        var p = WsjtxSetupImport.Plan(Settings(), [Flrig, Ic7300], [CodecIn], [CodecOut]);

        p.Callsign.Should().Be("K1ABC");
        p.Grid.Should().Be("FN42");
        p.AudioOnly.Should().BeFalse();
        p.Model.Should().Be(Ic7300);
        p.Port.Should().Be("COM4");
        p.Baud.Should().Be("19200");
        p.Ptt.Should().Be("cat");
        p.PttPort.Should().BeEmpty("\"CAT\" means the CAT port");
        p.Input.Should().Be(CodecIn);
        p.Output.Should().Be(CodecOut);
        p.NotCopied.Should().BeEmpty();
    }

    [Fact]
    public void Plan_UnknownRadioAndMissingSoundCard_SaysWhatToSetByHand()
    {
        var p = WsjtxSetupImport.Plan(Settings(rig: "Acme Rig-9000", soundOut: "Speakers (Gone)"), [Ic7300], [CodecIn], [CodecOut]);

        p.Model.Should().BeNull();
        p.Output.Should().BeNull();
        p.NotCopied.Should().HaveCount(2);
        p.NotCopied[0].Should().Contain("Acme Rig-9000");
        p.NotCopied[1].Should().Contain("Speakers (Gone)");
    }

    [Fact]
    public void Plan_NoCatRadio_AudioOnlyAndRtsWithoutCatBecomesVox()
    {
        var p = WsjtxSetupImport.Plan(Settings(rig: "None", ptt: "rts", pttPort: "COM5"), [Ic7300], [CodecIn], [CodecOut]);

        p.AudioOnly.Should().BeTrue();
        p.Model.Should().BeNull();
        p.Ptt.Should().Be("vox");
        p.NotCopied.Should().ContainSingle().Which.Should().Contain("RTS");
    }

    [Fact]
    public void Plan_FlrigOverNetwork_KeepsTheServerAddress()
    {
        var p = WsjtxSetupImport.Plan(Settings(rig: "FLRig FLRig", network: "127.0.0.1:12345"), [Flrig, Ic7300], [], []);

        p.Model.Should().Be(Flrig);
        p.NetworkAddress.Should().Be("127.0.0.1:12345");
    }

    [Fact]
    public void Plan_SeparatePttPort_Kept() =>
        WsjtxSetupImport.Plan(Settings(ptt: "dtr", pttPort: "COM7"), [Ic7300], [], []).PttPort.Should().Be("COM7");

    [Fact]
    public void MatchDevice_NameCutShort_MatchesByPrefix() =>
        WsjtxSetupImport.MatchDevice("Microphone (USB Audio", [new AudioDevice("x", "Line In"), CodecIn]).Should().Be(CodecIn);

    [Fact]
    public void Summary_SettingsAndLog_OneLine() =>
        WsjtxSetupImport.Summary(Settings(), 1234).Should().Be("WSJT-X is set up on this computer: K1ABC, FN42, Icom IC-7300 on COM4, 1,234 contacts in its log.");

    [Fact]
    public void CountRecords_CountsEorCaseInsensitively() =>
        WsjtxSetupImport.CountRecords("<eoh>\n<call:5>K1ABC <eor>\n<call:5>W1AW <EOR>\n").Should().Be(2);
}
