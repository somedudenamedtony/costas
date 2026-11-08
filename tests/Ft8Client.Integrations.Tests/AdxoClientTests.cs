// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Integrations.Ng3k;

namespace Ft8Client.Integrations.Tests;

public class AdxoClientTests
{
    // Shape of the feed as read on 2026-11-07, cut to three items and one that does not read.
    private const string Feed = """
        <?xml version="1.0" ?>
        <rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
        <channel>
        <title>ADXO</title>
        <item>
        <title>Solomon Is: Oct 3-Nov 20, 2026 -- H44MS -- QSL via: LoTW</title>
        <description>
        Oct 3-Nov 20, 2026 --
        Solomon Is --
        H44MS --
        QSL: LoTW --
        Source: OPDX (Sep 22, 2026) --
        By DL2GAC; 80-10m; mainly FT8, some SSB; QSL via DL2GAC (B/d)
        </description>
        <link>https://www.qrz.com/db/H44MS</link>
        </item>
        <item>
        <title>Falkland Is: Dec 12, 2026-Jan 2, 2027 -- VP8THW -- QSL via: LoTW</title>
        <description>x --</description>
        </item>
        <item>
        <title>Ceuta &amp; Melilla: Nov 13-15, 2026 -- EG9MM -- QSL via: LoTW</title>
        </item>
        <item><title>Date to be announced</title></item>
        </channel>
        </rss>
        """;

    [Fact]
    public void Parse_AdxoFeed_ReadsTheListings()
    {
        var list = AdxoClient.Parse(Feed)!;
        list.Select(d => d.Call).Should().Equal("H44MS", "VP8THW", "EG9MM");
        list[0].Info.Should().StartWith("By DL2GAC");
        list[1].LastDay.Should().Be(new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        list[2].Entity.Should().Be("Ceuta & Melilla");
    }

    [Theory]
    [InlineData("<html><body>Not found</body></html>")]
    [InlineData("not xml")]
    public void Parse_NotRss_Null(string text)
    {
        AdxoClient.Parse(text).Should().BeNull();
    }
}
