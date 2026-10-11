// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Tests;

public class DatabaseSessionLogTests
{
    private static readonly ContactLogEntry Entry = new("K1ABC", "FN42", -1, -13,
        new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc), new DateTime(2026, 1, 4, 2, 55, 0, DateTimeKind.Utc));

    private static readonly LogContext Context = new("20m", Mode.Ft8, 14_075_500, "W7LIT", "DN40", 25, new NeedResult(NeedTag.NewCall, 1),
        null, null, null, null, null);

    private static QsoRecord Write(Func<int?>? radioPower)
    {
        var db = new SqliteDatabase(Path.Combine(Directory.CreateTempSubdirectory("ft8log").FullName, "log.db"));
        var qsos = new QsoRepository(db);
        new DatabaseSessionLog(qsos, new UploadQueueRepository(db), "home", () => false, radioPower).Write(Entry, Context).Should().BeTrue();
        return qsos.List().Single();
    }

    [Fact]
    public void Write_Contact_CommentHasModeAndReports()
    {
        Write(null).Comment.Should().Be("FT8 Sent: -01 Rcvd: -13");
    }

    [Fact]
    public void Write_RadioReportsPower_LogsRadioPower()
    {
        Write(() => 40).TxPwr.Should().Be("40");
    }

    [Fact]
    public void Write_RadioCannotReportPower_LogsSettingsPower()
    {
        Write(() => null).TxPwr.Should().Be("25");
        Write(null).TxPwr.Should().Be("25");
    }
}
