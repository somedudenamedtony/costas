// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Journal;

namespace Ft8Client.Data.Tests;

public class DecodeJournalTests
{
    [Fact]
    public void Format_RxAndTx_MatchSpecExample()
    {
        var t = new DateTime(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc);
        DecodeJournal.Format(t, 14_074_000, false, "FT8", -8, 0.1, 1234, "CQ JA1QRS PM95")
            .Should().Be("260104_025345    14.074 Rx FT8     -8  0.1 1234 CQ JA1QRS PM95");
        DecodeJournal.Format(t.AddSeconds(15), 14_074_000, true, "FT8", 0, 0, 1650, "JA1QRS W7LIT DN40")
            .Should().Be("260104_025400    14.074 Tx FT8      0  0.0 1650 JA1QRS W7LIT DN40");
    }

    [Fact]
    public void ReadLines_TwoMonths_StreamsOldestMonthFirstAndParses()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ft8j-" + Guid.NewGuid().ToString("N"));
        try
        {
            var j = new DecodeJournal(dir);
            var feb = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var jan = new DateTime(2026, 1, 31, 23, 59, 45, DateTimeKind.Utc);
            j.Append(feb, [DecodeJournal.Format(feb, 7_074_000, false, "FT8", -3, -0.4, 900, "CQ JA1QRS PM95")]);
            j.Append(jan, ["a", "b"]);
            File.WriteAllText(Path.Combine(dir, "other.txt"), "ignored\n");

            var lines = j.ReadLines(TestContext.Current.CancellationToken).ToList();

            lines.Should().HaveCount(3);
            lines[0].Should().Be("a");
            Core.History.JournalLine.TryParse(lines[2], out var parsed).Should().BeTrue();
            parsed!.DialHz.Should().Be(7_074_000);
            parsed.Message.Should().Be("CQ JA1QRS PM95");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReadLines_NoFolder_ReturnsNothing()
    {
        new DecodeJournal(Path.Combine(Path.GetTempPath(), "ft8j-missing-" + Guid.NewGuid().ToString("N"))).ReadLines(TestContext.Current.CancellationToken).Should().BeEmpty();
    }

    [Fact]
    public void Append_TwoMonths_GoInMonthlyFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ft8j-" + Guid.NewGuid().ToString("N"));
        try
        {
            var j = new DecodeJournal(dir);
            j.Append(new DateTime(2026, 1, 31, 23, 59, 45, DateTimeKind.Utc), ["a", "b"]);
            j.Append(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), ["c"]);
            j.Append(new DateTime(2026, 2, 1, 0, 0, 15, DateTimeKind.Utc), []);
            File.ReadAllText(Path.Combine(dir, "decodes-2026-01.txt")).Should().Be("a\nb\n");
            File.ReadAllText(Path.Combine(dir, "decodes-2026-02.txt")).Should().Be("c\n");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
