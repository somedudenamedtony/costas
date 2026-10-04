// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Time;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Integrations.Fakes;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.Qrz;

namespace Ft8Client.Integrations.Tests;

public sealed class QrzTests : IDisposable
{
    private const string Key = "ABCD-1234-ABCD-1234";
    private static readonly CountryFile Countries = ReferenceData.LoadCountries();
    private readonly string _dir = Directory.CreateTempSubdirectory("ft8qrz").FullName;
    private readonly ManualClock _clock = new(new DateTime(2026, 1, 4, 3, 0, 0, DateTimeKind.Utc));
    private readonly FakeQrzServer _server;
    private readonly HttpClient _http;

    public QrzTests()
    {
        _server = new FakeQrzServer(Key, "W7LIT") { Now = () => _clock.UtcNow };
        _http = HttpFactory.Create("W7LIT", _server);
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }

    private SqliteDatabase Db() => new(Path.Combine(_dir, "log.db"));

    private QrzLogbookClient Client() => new(_http);

    [Fact]
    public void Parse_RecordedSamples_MatchObservedShapes()
    {
        var status = QrzResponse.Parse(File.ReadAllText(Path.Combine(TestPaths.Samples, "qrz", "status.txt")));
        status.Result.Should().Be("OK");
        status["COUNT"].Should().Be("396");
        status["BOOK_NAME"].Should().Be("W7LIT Logbook");

        var fetch = QrzResponse.Parse(File.ReadAllText(Path.Combine(TestPaths.Samples, "qrz", "fetch-page.txt")));
        fetch.Count.Should().Be(2);
        var records = Data.Adif.AdifReader.Read(fetch.Adif!);
        records.Should().HaveCount(2);
        records[0]["name"].Should().Be("Ann & Bo");
        records[0]["app_qrzlog_logid"].Should().Be("1067389545");
        Data.Adif.AdifMapper.IsConfirmed(records[0]).Should().BeTrue();
        Data.Adif.AdifMapper.IsConfirmed(records[1]).Should().BeFalse();

        var auth = QrzResponse.Parse(File.ReadAllText(Path.Combine(TestPaths.Samples, "qrz", "auth-fail.txt")));
        auth.Result.Should().Be("AUTH");
        auth.Reason.Should().StartWith("invalid api key");
    }

    [Fact]
    public async Task Status_SendsUserAgentAndReturnsTotals()
    {
        _server.Add("K1ABC", _clock.UtcNow.AddDays(-1), confirmed: true);
        var s = await Client().StatusAsync(Key, TestContext.Current.CancellationToken);
        s.Count.Should().Be(1);
        s.Confirmed.Should().Be(1);
        _http.DefaultRequestHeaders.UserAgent.ToString().Should().StartWith("Costas/").And.EndWith("(W7LIT)");
    }

    [Fact]
    public async Task Status_BadKey_ThrowsAuth()
    {
        var act = () => Client().StatusAsync("WRONG", TestContext.Current.CancellationToken);
        (await act.Should().ThrowAsync<QrzException>()).Which.IsAuth.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_FullThenIncremental_PagesAndPicksUpConfirmations()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ids = new List<long>();
        for (var i = 0; i < 612; i++)
        {
            ids.Add(_server.Add($"K{i % 10}A{(char)('A' + i % 26)}{(char)('A' + i / 26 % 26)}", start.AddHours(i), grid: "FN42", modifiedUtc: start.AddHours(i)));
        }
        var db = Db();
        var repo = new QsoRepository(db);
        var sync = new QrzSyncService(Client(), repo, new SyncStateRepository(db), Countries, _clock);
        var progress = new List<int>();

        var first = await sync.SyncAsync("home", "W7LIT", Key, new Progress<int>(progress.Add), TestContext.Current.CancellationToken);

        first.Full.Should().BeTrue();
        first.Fetched.Should().Be(612);
        first.Inserted.Should().Be(612);
        repo.Count().Should().Be(612);
        var fetches = _server.Requests.Where(r => r["ACTION"] == "FETCH").Select(r => r["OPTION"]).ToList();
        fetches.Should().HaveCount(3);
        fetches[0].Should().Be("MAX:250,AFTERLOGID:0");
        fetches[1].Should().Be($"MAX:250,AFTERLOGID:{ids[249] + 1}");

        // Later: one confirmation and one new contact.
        _clock.Advance(TimeSpan.FromDays(3));
        _server.Confirm(ids[10]);
        _server.Add("ZL2RPA", _clock.UtcNow.AddHours(-1), grid: "RF70");
        _server.Requests.Clear();

        var second = await sync.SyncAsync("home", "W7LIT", Key, null, TestContext.Current.CancellationToken);

        second.Full.Should().BeFalse();
        second.Fetched.Should().Be(2);
        second.Inserted.Should().Be(1);
        second.Merged.Should().Be(1);
        _server.Requests.Single()["OPTION"].Should().Be("MODSINCE:2026-01-03,MAX:250,AFTERLOGID:0");
        repo.List().Single(q => q.QrzLogid == ids[10]).Confirmed.Should().BeTrue();
        repo.Count().Should().Be(613);
    }

    [Fact]
    public async Task Sync_AuthFailure_Throws()
    {
        var db = Db();
        var sync = new QrzSyncService(Client(), new QsoRepository(db), new SyncStateRepository(db), Countries, _clock);
        var act = () => sync.SyncAsync("home", "W7LIT", "BAD", null, TestContext.Current.CancellationToken);
        (await act.Should().ThrowAsync<QrzException>()).Which.IsAuth.Should().BeTrue();
    }

    private static QsoRecord Local(string call, DateTime on) => new()
    {
        ProfileId = "home", Call = call, QsoDateOn = on, Band = "20m", Mode = "FT8", StationCallsign = "W7LIT",
        RstSent = "-10", RstRcvd = "-12", Source = QsoSource.Local,
    };

    [Fact]
    public async Task Upload_SurvivesRestart_BacksOff_AndRateLimits()
    {
        var delays = new List<TimeSpan>();
        Task Delay(TimeSpan t, CancellationToken _)
        {
            delays.Add(t);
            _clock.Advance(t);
            return Task.CompletedTask;
        }

        // Session 1: two contacts queued, QRZ down.
        long a, b;
        {
            var db = Db();
            var repo = new QsoRepository(db);
            a = repo.Insert(Local("JA1QRS", _clock.UtcNow));
            b = repo.Insert(Local("VE7HLB", _clock.UtcNow.AddMinutes(2)));
            var svc = new QrzUploadService(Client(), repo, new UploadQueueRepository(db), _clock, delay: Delay);
            svc.Enqueue(a);
            svc.Enqueue(b);
            _server.FailNext = 1;
            (await svc.ProcessDueAsync(Key, TestContext.Current.CancellationToken)).Should().Be(0);
            svc.Status.Health.Should().Be(Core.Services.ServiceHealth.Degraded);
            repo.Get(a)!.UploadState.Should().Be(UploadState.Queued);
            var item = new UploadQueueRepository(db).All().First(i => i.QsoId == a);
            item.Attempts.Should().Be(1);
            item.NextTryUtc.Should().BeAfter(_clock.UtcNow);
            (await svc.ProcessDueAsync(Key, TestContext.Current.CancellationToken)).Should().Be(1, "only the item that is due is tried");
        }

        // Session 2 (restart): the backed-off item is still there and uploads when due.
        _clock.Advance(TimeSpan.FromMinutes(1));
        {
            var db = Db();
            var repo = new QsoRepository(db);
            var queue = new UploadQueueRepository(db);
            queue.Count().Should().Be(1);
            var svc = new QrzUploadService(Client(), repo, queue, _clock, delay: Delay);
            (await svc.ProcessDueAsync(Key, TestContext.Current.CancellationToken)).Should().Be(1);
            queue.Count().Should().Be(0);
            repo.Get(a)!.UploadState.Should().Be(UploadState.Uploaded);
            repo.Get(a)!.QrzLogid.Should().NotBeNull();
        }

        _server.Requests.Where(r => r["ACTION"] == "INSERT").Should().OnlyContain(r => !r.ContainsKey("OPTION"), "REPLACE is never sent");
        var times = _server.RequestTimes;
        for (var i = 1; i < times.Count; i++) (times[i] - times[i - 1]).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Upload_Duplicate_CountsAsUploaded()
    {
        var on = new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
        _server.Add("ZL2RPA", on);
        var db = Db();
        var repo = new QsoRepository(db);
        var id = repo.Insert(Local("ZL2RPA", on));
        var svc = new QrzUploadService(Client(), repo, new UploadQueueRepository(db), _clock, delay: (_, _) => Task.CompletedTask);
        svc.Enqueue(id);
        (await svc.ProcessDueAsync(Key, TestContext.Current.CancellationToken)).Should().Be(1);
        repo.Get(id)!.UploadState.Should().Be(UploadState.Uploaded);
        _server.Count.Should().Be(1);
    }

    [Fact]
    public async Task Upload_Rejected_KeepsReasonVerbatim()
    {
        _server.RejectReason = "QSO date is outside the date range of this logbook";
        var db = Db();
        var repo = new QsoRepository(db);
        var id = repo.Insert(Local("ZL2RPA", _clock.UtcNow));
        var svc = new QrzUploadService(Client(), repo, new UploadQueueRepository(db), _clock, delay: (_, _) => Task.CompletedTask);
        svc.Enqueue(id);
        await svc.ProcessDueAsync(Key, TestContext.Current.CancellationToken);
        var row = repo.Get(id)!;
        row.UploadState.Should().Be(UploadState.Failed);
        row.UploadError.Should().Be("QSO date is outside the date range of this logbook");
    }

    [Fact]
    public async Task Upload_BadKey_StaysQueuedAndReportsDown()
    {
        var db = Db();
        var repo = new QsoRepository(db);
        var id = repo.Insert(Local("ZL2RPA", _clock.UtcNow));
        var svc = new QrzUploadService(Client(), repo, new UploadQueueRepository(db), _clock, delay: (_, _) => Task.CompletedTask);
        svc.Enqueue(id);
        await svc.ProcessDueAsync("WRONG", TestContext.Current.CancellationToken);
        repo.Get(id)!.UploadState.Should().Be(UploadState.Queued);
        svc.Status.Health.Should().Be(Core.Services.ServiceHealth.Down);
    }

    [Fact]
    public async Task Lookup_CachesSessionAndResults_RelogsOnceOnExpiry_StripsSuffix()
    {
        _server.AddLookup("ZL2RPA", "Bob Smith", "Wellington", null, "New Zealand", "RE78");
        var db = Db();
        var cache = new CallsignCacheRepository(db);
        var xml = new QrzXmlClient(_http, () => ("user", "pass"));
        var svc = new CallsignLookupService(xml, cache, _clock);
        var ct = TestContext.Current.CancellationToken;

        var r = await svc.LookupAsync("ZL2RPA", ct);
        r!.Name.Should().Be("Bob Smith");
        r.City.Should().Be("Wellington");
        r.Grid.Should().Be("RE78");
        _server.XmlLogins.Should().Be(1);

        (await svc.LookupAsync("zl2rpa", ct))!.Name.Should().Be("Bob Smith");
        _server.RequestTimes.Should().HaveCount(2, "the second lookup came from the cache");

        _server.ExpireSession();
        (await svc.LookupAsync("ZL2RPA/P", ct))!.Grid.Should().Be("RE78");
        _server.XmlLogins.Should().Be(2);

        (await svc.LookupAsync("N0CALL", ct)).Should().BeNull();
        cache.GetFresh("N0CALL", _clock.UtcNow)!.Found.Should().BeFalse();
        cache.GetFresh("N0CALL", _clock.UtcNow.AddDays(8)).Should().BeNull();
    }
}
