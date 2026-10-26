// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;

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
    public void VelopackSetupCommand_PathsWithSpaces_RunsSetupSilentlyAndReopensClassicAppOnlyOnFailure()
    {
        var psi = UpdateService.VelopackSetupCommand(
            @"C:\Users\A & B\AppData\Local\Costas\tmp\updates\CostasApp-win-Setup.exe",
            @"C:\Users\A & B\AppData\Local\Costas\logs\update-20261026-150000.log",
            @"C:\Users\A & B\AppData\Local\Programs\Costas\Costas.exe",
            @"C:\Users\A & B\AppData\Local\Costas\tmp\updates");

        psi.FileName.Should().EndWith("cmd.exe");
        psi.Arguments.Should().Be(
            "/d /s /c \"start \"\" /wait \"C:\\Users\\A & B\\AppData\\Local\\Costas\\tmp\\updates\\CostasApp-win-Setup.exe\" " +
            "--silent --log \"C:\\Users\\A & B\\AppData\\Local\\Costas\\logs\\update-20261026-150000.log\" " +
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
}
