// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.Core;

namespace Ft8Client.App.Tests;

public class SimulatedSlotSourceTests
{
    [Theory]
    [InlineData("psk31")]
    [InlineData("rtty")]
    public void Constructor_KeyboardFolder_GivesSilentSlots(string folder)
    {
        var source = new SimulatedSlotSource(Path.Combine(TestPaths.Samples, folder));
        var slot = new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc);

        source.Files.Should().BeEmpty();
        source.BeginSlot(slot, Mode.Ft8);
        source.Cutoff(slot, Mode.Ft8)!.Samples12k.Should().OnlyContain(s => s == 0);
    }

    [Fact]
    public void Constructor_FolderWithNoWav_Throws()
    {
        var empty = Directory.CreateTempSubdirectory("costas-nowav");
        try
        {
            var act = () => new SimulatedSlotSource(empty.FullName);
            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            empty.Delete();
        }
    }
}
