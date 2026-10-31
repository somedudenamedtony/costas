// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.Core.Bands;

namespace Ft8Client.Core.Tests;

public class FrequencyTableTests
{
    private static readonly FrequencyTable Table = new(
    [
        new FrequencyEntry("20m", Mode.Ft8, 14_074_000, 14_000_000, 14_350_000),
        new FrequencyEntry("20m", Mode.Rtty, 14_080_000, 14_000_000, 14_350_000),
        new FrequencyEntry("20m", Mode.Psk31, 14_070_000, 14_000_000, 14_350_000),
        new FrequencyEntry("60m", Mode.Ft8, 5_357_000, 5_351_500, 5_366_500),
    ]);

    [Fact]
    public void RttyBandwidthHz_StandardRtty_ShiftPlusTwiceBaud()
    {
        ModeInfo.RttyBandwidthHz(170, 45.45).Should().Be(261);
        ModeInfo.RttyBandwidthHz(850, 45.45).Should().Be(941);
    }

    [Fact]
    public void TransmitAllowed_WideShiftNearTopEdge_Refused()
    {
        // 850 Hz shift centred 300 Hz below the edge: mark sits 125 Hz past it. The fixed 250 Hz width let it through.
        // Band edges only: the US data segments would refuse 14.348 MHz whatever the width.
        var table = new FrequencyTable(Table.Entries) { UsDataSegmentsOnly = false };
        const long dial = 14_348_000;
        const int offset = 1_700;
        table.TransmitAllowed("20m", Mode.Rtty, dial, offset, out _).Should().BeTrue();
        table.TransmitAllowed("20m", Mode.Rtty, dial, offset, ModeInfo.RttyBandwidthHz(850, 45.45), out var why).Should().BeFalse();
        why.Should().Contain("outside");
    }

    [Fact]
    public void TransmitAllowed_WideShiftNearTopOfUsDataSegment_Refused()
    {
        // 20 m data ends at 14.150 MHz for US stations: a standard RTTY signal centred 300 Hz below it fits, an 850 Hz
        // shift does not.
        const long dial = 14_148_000;
        const int offset = 1_700;
        Table.TransmitAllowed("20m", Mode.Rtty, dial, offset, out _).Should().BeTrue();
        Table.TransmitAllowed("20m", Mode.Rtty, dial, offset, ModeInfo.RttyBandwidthHz(850, 45.45), out _).Should().BeFalse();
    }

    [Fact]
    public void TransmitAllowed_PskOnBandWithOnlyFt8Row_RefusedWithReason()
    {
        Table.TransmitAllowed("60m", Mode.Psk31, 5_357_000, 1_000, out var why).Should().BeFalse();
        why.Should().Contain("no PSK31 row").And.Contain("60 m");
        Table.TransmitAllowed("60m", Mode.Rtty, 5_357_000, 1_000, out _).Should().BeFalse();
    }

    [Fact]
    public void TransmitAllowed_Ft8OnItsOwnRow_Allowed()
    {
        Table.TransmitAllowed("60m", Mode.Ft8, 5_357_000, 1_000, out _).Should().BeTrue();
        Table.TransmitAllowed("20m", Mode.Psk31, 14_070_000, 1_000, out _).Should().BeTrue();
    }
}
