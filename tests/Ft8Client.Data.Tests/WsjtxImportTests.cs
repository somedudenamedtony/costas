// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Import;

namespace Ft8Client.Data.Tests;

public class WsjtxImportTests
{
    [Fact]
    public void Parse_SampleIni_ReadsStationRadioAndAudio()
    {
        var s = WsjtxSettingsReader.Parse(File.ReadAllText(Path.Combine(TestPaths.Samples, "wsjtx", "WSJT-X.ini")))!;

        s.MyCall.Should().Be("K1ABC");
        s.MyGrid.Should().Be("FN42");
        s.RigName.Should().Be("Icom IC-7300");
        s.SerialPort.Should().Be("COM4");
        s.NetworkAddress.Should().BeEmpty();
        s.Baud.Should().Be(19200);
        s.Ptt.Should().Be("cat", "the enum name inside the @Variant blob");
        s.PttPort.Should().Be("CAT");
        s.SoundIn.Should().Be("Microphone (USB Audio CODEC )");
        s.SoundOut.Should().Be("Speakers (USB Audio CODEC )", "quotes are taken off");
    }

    [Theory]
    [InlineData("PTTMethod=@Variant(\\0\\0\\0\\x7f\\0PTT_method_RTS\\0)", "rts")]
    [InlineData("PTTMethod=PTT_method_DTR", "dtr")]
    [InlineData("", "vox")]
    public void Parse_PttMethod_MapsToCostasNames(string line, string expected) =>
        WsjtxSettingsReader.Parse($"[Configuration]\nMyCall=K1ABC\n{line}\n")!.Ptt.Should().Be(expected);

    [Fact]
    public void Parse_EscapesAndCrLf_Unescaped() =>
        WsjtxSettingsReader.Parse("[Configuration]\r\nSoundInName=\"Line \\\"1\\\" \\x00e9\"\r\n")!.SoundIn.Should().Be("Line \"1\" é");

    [Fact]
    public void Parse_NoConfigurationGroup_Null() =>
        WsjtxSettingsReader.Parse("[Common]\nMode=FT8\n").Should().BeNull();

    [Fact]
    public void Find_Linux_SettingsInConfigAndLogInDataFolder()
    {
        var root = Directory.CreateTempSubdirectory("wsjtxfind").FullName;
        var config = Path.Combine(root, "config");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(Path.Combine(data, "WSJT-X"));
        File.WriteAllText(Path.Combine(config, "WSJT-X.ini"), "[Configuration]\n");
        File.WriteAllText(Path.Combine(data, "WSJT-X", "wsjtx_log.adi"), "<eoh>\n");

        var found = WsjtxInstall.Find(windows: false, null, config, data)!;

        found.SettingsPath.Should().Be(Path.Combine(config, "WSJT-X.ini"));
        found.LogPath.Should().Be(Path.Combine(data, "WSJT-X", "wsjtx_log.adi"));
    }

    [Fact]
    public void Find_Windows_BothInLocalAppDataWsjtx()
    {
        var local = Directory.CreateTempSubdirectory("wsjtxwin").FullName;
        Directory.CreateDirectory(Path.Combine(local, "WSJT-X"));
        File.WriteAllText(Path.Combine(local, "WSJT-X", "wsjtx_log.adi"), "<eoh>\n");

        var found = WsjtxInstall.Find(windows: true, local, null, null)!;

        found.SettingsPath.Should().BeNull();
        found.LogPath.Should().Be(Path.Combine(local, "WSJT-X", "wsjtx_log.adi"));
    }

    [Fact]
    public void Find_NothingThere_Null() =>
        WsjtxInstall.Find(windows: false, null, Directory.CreateTempSubdirectory("wsjtxnone").FullName, null).Should().BeNull();
}
