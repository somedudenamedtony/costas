// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Messages;

namespace Ft8Client.App.Tests;

public class RawDecodesTests
{
    private static RawDecodeLine Line(DateTime slot, string text, int hz = 1000) =>
        new(new Decode(slot, -10, 0.1, hz, text, false), MessageParser.Parse(text), null, false);

    [Fact]
    public void RawLine_LocalZone_ShowsUtcAndLocalTimes()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");
        var line = new RawLineViewModel(Line(new DateTime(2026, 10, 11, 2, 54, 15, DateTimeKind.Utc), "CQ K1ABC FN42"), zone);
        line.Utc.Should().Be("025415");
        line.Local.Should().Be("215415");
    }

    [Fact]
    public void Apply_SeveralSlots_NewestSlotFirstWithArrivalOrderInsideSlot()
    {
        var t0 = new DateTime(2026, 10, 11, 2, 54, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(15);
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply([Line(t0, "CQ K1ABC FN42", 900), Line(t0, "CQ W9XYZ EN52", 1200), Line(t1, "CQ N0CALL EM12", 500), Line(t1, "CQ G4AAA IO91", 2000)]);
        vm.Lines.Select(l => l.IsBlank ? "-" : l.Message).Should().Equal(
            "CQ N0CALL EM12", "CQ G4AAA IO91", "-", "CQ K1ABC FN42", "CQ W9XYZ EN52");
    }
}
