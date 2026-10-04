// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Decoding;

namespace Ft8Client.Decoding.Tests;

public class WavArchiveTests
{
    [Theory]
    [InlineData("none", 5, false)]
    [InlineData("decoded", 0, false)]
    [InlineData("decoded", 1, true)]
    [InlineData("all", 0, true)]
    [InlineData("bogus", 3, false)]
    public void ShouldSave_Setting_Decides(string setting, int decodes, bool expected) =>
        WavArchive.ShouldSave(setting, decodes).Should().Be(expected);

    [Fact]
    public void SaveAndPrune_OldFiles_DeletedOthersKept()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ft8w-" + Guid.NewGuid().ToString("N"));
        try
        {
            var a = new WavArchive(dir);
            var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            var old = a.Save(new SlotAudio(now.AddDays(-31), Mode.Ft8, new short[180_000]));
            var recent = a.Save(new SlotAudio(now.AddDays(-29), Mode.Ft8, new short[180_000]));
            File.WriteAllText(Path.Combine(dir, "notes.wav"), "not a slot");
            Path.GetFileName(recent).Should().Be("260131_120000.wav");
            WavFile.Read(recent).Samples.Length.Should().Be(180_000);
            a.Prune(now, 30).Should().Be(1);
            File.Exists(old).Should().BeFalse();
            File.Exists(recent).Should().BeTrue();
            File.Exists(Path.Combine(dir, "notes.wav")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
