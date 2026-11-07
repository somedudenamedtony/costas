// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;
using Ft8Client.Integrations.Updates;

namespace Ft8Client.App.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public void InstallAndReopenCommand_PathsWithSpacesAndAmpersands_RunsInstallerThenReopensAppInHiddenCmd()
    {
        var psi = UpdateService.InstallAndReopenCommand(
            @"C:\Users\A & B\AppData\Local\Costas\tmp\updates\Costas-Setup-0.1.0.90.exe",
            @"C:\Users\A & B\AppData\Local\Costas\logs\update-20261012-150000.log",
            @"C:\Users\A & B\AppData\Local\Programs\Costas\Costas.exe",
            @"C:\Users\A & B\AppData\Local\Costas\tmp\updates");

        psi.FileName.Should().EndWith("cmd.exe");
        psi.Arguments.Should().Be(
            "/d /s /c \"start \"\" /wait \"C:\\Users\\A & B\\AppData\\Local\\Costas\\tmp\\updates\\Costas-Setup-0.1.0.90.exe\" " +
            "/SILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL \"/LOG=C:\\Users\\A & B\\AppData\\Local\\Costas\\logs\\update-20261012-150000.log\" " +
            "& start \"\" \"C:\\Users\\A & B\\AppData\\Local\\Programs\\Costas\\Costas.exe\"\"");
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeTrue();
        psi.WorkingDirectory.Should().Be(@"C:\Users\A & B\AppData\Local\Costas\tmp\updates");
    }

    [Fact]
    public void VelopackSetupCommand_PathsWithSpaces_RunsSetupSilentlyThenStartsNewAppOrReopensClassicApp()
    {
        var psi = UpdateService.VelopackSetupCommand(
            @"C:\Users\A & B\AppData\Local\Costas\tmp\updates\CostasApp-win-Setup.exe",
            @"C:\Users\A & B\AppData\Local\Costas\logs\update-20261026-150000.log",
            @"C:\Users\A & B\AppData\Local\CostasApp\current\Costas.exe",
            @"C:\Users\A & B\AppData\Local\Programs\Costas\Costas.exe",
            @"C:\Users\A & B\AppData\Local\Costas\tmp\updates");

        psi.FileName.Should().EndWith("cmd.exe");
        psi.Arguments.Should().Be(
            "/d /s /c \"start \"\" /wait \"C:\\Users\\A & B\\AppData\\Local\\Costas\\tmp\\updates\\CostasApp-win-Setup.exe\" " +
            "--silent --log \"C:\\Users\\A & B\\AppData\\Local\\Costas\\logs\\update-20261026-150000.log\" " +
            "&& start \"\" \"C:\\Users\\A & B\\AppData\\Local\\CostasApp\\current\\Costas.exe\" " +
            "|| start \"\" \"C:\\Users\\A & B\\AppData\\Local\\Programs\\Costas\\Costas.exe\"\"");
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "full")]
    [InlineData(2_516_582, "2.4 MB")]
    [InlineData(99_614_720, "95 MB")]
    public void DownloadSize_Bytes_ShortMegabytes(long bytes, string text) =>
        Ft8Client.App.ViewModels.UpdateBarViewModel.DownloadSize(bytes).Should().Be(text);

    private static ReleaseInfo Build(string version, bool development = false) =>
        new(Version.Parse(version), $"v{version}", string.Empty, "CostasApp-win-Setup.exe", string.Empty, 1, "ab") { IsDevelopment = development };

    [Theory]
    [InlineData("0.1.300", "0.1.305", false, false)] // release, older than a development build, development builds on
    [InlineData("0.1.300", "0.1.305", true, true)] // ...and turned off: go back to it
    [InlineData("0.1.305", "0.1.305", true, false)] // the same build: nothing to go back to
    [InlineData("0.1.310", "0.1.305", false, true)] // newer: offered as ever
    public void ShouldOffer_Release_OlderOnlyWhenLeavingDevelopmentBuilds(string release, string running, bool leaving, bool offered) =>
        UpdateService.ShouldOffer(Build(release), running, leaving).Should().Be(offered);

    [Fact]
    public void ShouldOffer_OlderDevelopmentBuildWhileLeaving_NotOffered() =>
        UpdateService.ShouldOffer(Build("0.1.300", development: true), "0.1.305", leavingDevelopment: true).Should().BeFalse();

    [Fact]
    public void BarText_DowngradeAndDevelopment_SayWhatTheyAre()
    {
        Ft8Client.App.ViewModels.UpdateBarViewModel.BarText(Build("0.1.300") with { IsDowngrade = true }, "0.1.305", "95 MB")
            .Should().Be("Development builds are off: go back to release 0.1.300 (you have development build 0.1.305; 95 MB download).");
        Ft8Client.App.ViewModels.UpdateBarViewModel.BarText(Build("0.1.310", development: true), "0.1.305", "95 MB")
            .Should().StartWith("Development build 0.1.310 is available");
    }
}
