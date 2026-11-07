// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Dxpeditions;

namespace Ft8Client.Core.Tests;

public class DxpeditionCalendarTests
{
    private static DateTime D(int y, int m, int d, int h = 0) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Oct 3-Nov 20, 2026", 2026, 10, 3, 2026, 11, 20)]
    [InlineData("Nov 3-16, 2026", 2026, 11, 3, 2026, 11, 16)]
    [InlineData("Dec 12, 2026-Jan 2, 2027", 2026, 12, 12, 2027, 1, 2)]
    [InlineData("Dec 28-Jan 3, 2027", 2026, 12, 28, 2027, 1, 3)]
    public void ParseDates_AdxoRanges_Parse(string text, int y1, int m1, int d1, int y2, int m2, int d2)
    {
        DxpeditionCalendar.ParseDates(text).Should().Be((D(y1, m1, d1), D(y2, m2, d2)));
    }

    [Theory]
    [InlineData("Soon")]
    [InlineData("Feb 30-31, 2027")]
    [InlineData("Nov 16-3, 2026")]
    public void ParseDates_Nonsense_Null(string text)
    {
        DxpeditionCalendar.ParseDates(text).Should().BeNull();
    }

    [Fact]
    public void ParseItem_AdxoTitleAndDescription_ReadsEveryField()
    {
        const string description = "\nOct 3-Nov 20, 2026 --\nSolomon Is --\nH44MS --\nQSL: LoTW --\nSource: OPDX (Sep 22, 2026) --\n"
                                   + "By DL2GAC; 80-10m; mainly FT8, some SSB; QSL via DL2GAC (B/d)\n";
        var d = DxpeditionCalendar.ParseItem("Solomon Is: Oct 3-Nov 20, 2026 -- H44MS -- QSL via: LoTW", description)!;
        d.Should().Be(new Dxpedition("H44MS", "Solomon Is", D(2026, 10, 3), D(2026, 11, 20), "LoTW",
            "By DL2GAC; 80-10m; mainly FT8, some SSB; QSL via DL2GAC (B/d)"));
        d.MentionsFt8.Should().BeTrue();
    }

    [Fact]
    public void ParseItem_UnknownQslAndNoNotes_LeftEmpty()
    {
        var d = DxpeditionCalendar.ParseItem("Cocos (Keeling) I: Nov 20-27, 2026 -- VK9DS -- QSL via: ?", "Nov 20-27, 2026 --\nSource: x --")!;
        (d.Entity, d.Call, d.Qsl, d.Info).Should().Be(("Cocos (Keeling) I", "VK9DS", "", ""));
    }

    [Fact]
    public void ParseItem_NotAListing_Null()
    {
        DxpeditionCalendar.ParseItem("ADXO", null).Should().BeNull();
    }

    [Fact]
    public void OnAirOn_LastDay_CountsWholeDay()
    {
        var d = new Dxpedition("H44MS", "Solomon Is", D(2026, 10, 3), D(2026, 11, 20), "", "");
        d.OnAirOn(D(2026, 10, 2, 23)).Should().BeFalse();
        d.OnAirOn(D(2026, 10, 3)).Should().BeTrue();
        d.OnAirOn(D(2026, 11, 20, 23)).Should().BeTrue();
        d.OnAirOn(D(2026, 11, 21)).Should().BeFalse();
    }

    [Theory]
    [InlineData("H44MS", "H44MS", true)]
    [InlineData("H44MS", "h44ms", true)]
    [InlineData("3B9", "3B9/M0CFW", true)]
    [InlineData("3B9", "3B9KW", false)]
    [InlineData("3B9", "M0CFW", false)]
    [InlineData("VP2V", "VP2V/K1ABC", true)]
    [InlineData("H44MS", "H44MS/P", true)]
    [InlineData("H44MS", "H44M", false)]
    public void Matches_ListedCallOrPortableSide(string listed, string heard, bool match)
    {
        new Dxpedition(listed, "x", D(2026, 1, 1), D(2026, 1, 2), "", "").Matches(heard).Should().Be(match);
    }

    [Fact]
    public void Calendar_OnAirStartingSoonAndMatch_UseTheDate()
    {
        var cal = new DxpeditionCalendar([
            new Dxpedition("H44MS", "Solomon Is", D(2026, 10, 3), D(2026, 11, 20), "", ""),
            new Dxpedition("D44PX", "Cape Verde Is", D(2026, 11, 4), D(2026, 11, 11), "", ""),
            new Dxpedition("E51A", "South Cook Is", D(2026, 11, 14), D(2026, 11, 22), "", ""),
            new Dxpedition("3Y0L", "Peter I", D(2027, 2, 1), D(2027, 2, 28), "", ""),
        ], D(2026, 11, 7));
        var now = D(2026, 11, 7, 20);
        cal.OnAir(now).Select(d => d.Call).Should().Equal("D44PX", "H44MS");
        cal.StartingSoon(now, 14).Select(d => d.Call).Should().Equal("E51A");
        cal.Match("D44PX", now)!.Entity.Should().Be("Cape Verde Is");
        cal.Match("E51A", now).Should().BeNull("it has not started yet");
    }

    [Theory]
    [InlineData("By M0CFW as 3B9/M0CFW; 160-6m; CW SSB + digital; QRV for CQWW DX SSB using 3B9KW", "3B9/M0CFW,3B9KW")]
    [InlineData("By IV3FSG I1FQH as 6W/I2YSB and 6W/IK2HKT; 160-6m", "6W/I2YSB,6W/IK2HKT")]
    [InlineData("By JM1LIG JO3LVG JG1LFF as T88FM T88MK T88XM fm Koror I", "T88FM,T88MK,T88XM")]
    [InlineData("By K0YA as V47YA and W5RCX as V47RCX fm St Kitts I", "V47YA,V47RCX")]
    [InlineData("By DL2GAC; 80-10m; mainly FT8, some SSB; QSL via DL2GAC (B/d)", "")]
    public void SignsAs_CallsAfterAsOrUsing(string info, string calls)
    {
        new Dxpedition("X", "x", D(2026, 1, 1), D(2026, 1, 2), "", info).SignsAs()
            .Should().Equal(calls.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Match_CallFromTheNotes_FindsTheOperation()
    {
        var cal = new DxpeditionCalendar([new Dxpedition("3B9", "Rodrigues I", D(2026, 10, 20), D(2026, 11, 28), "LoTW",
            "By M0CFW as 3B9/M0CFW; 160-6m; using 3B9KW")], D(2026, 11, 7));
        cal.Match("3B9KW", D(2026, 11, 7))!.Entity.Should().Be("Rodrigues I");
        cal.Match("M0CFW", D(2026, 11, 7)).Should().BeNull("the home call alone is not the DXpedition");
    }

    [Fact]
    public void Text_RowDetailsAndWhen()
    {
        var d = new Dxpedition("H44MS", "Solomon Is", D(2026, 10, 3), D(2026, 11, 20), "LoTW", "By DL2GAC; 80-10m");
        var now = D(2026, 11, 7, 20);
        DxpeditionText.Why(d).Should().Be("DXpedition · Solomon Is");
        DxpeditionText.Why(null).Should().BeEmpty();
        DxpeditionText.Details(d, now).Should().Be("DXpedition H44MS · Solomon Is · Oct 3 – Nov 20 · QSL LoTW\nBy DL2GAC; 80-10m");
        DxpeditionText.When(d, now).Should().Be("13 days left");
        DxpeditionText.When(d, D(2026, 11, 20, 3)).Should().Be("Ends today");
        DxpeditionText.When(d with { FirstDay = D(2026, 11, 8) }, now).Should().Be("Starts tomorrow");
        DxpeditionText.Dates(d with { LastDay = D(2027, 1, 2) }, now).Should().Be("Oct 3 – Jan 2, 2027");
    }
}
