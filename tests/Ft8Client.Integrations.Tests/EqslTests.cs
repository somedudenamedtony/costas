// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Integrations.Eqsl;
using Ft8Client.Integrations.Net;

namespace Ft8Client.Integrations.Tests;

public sealed class EqslTests
{
    private static readonly DateTime Now = new(2026, 1, 4, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("<html><body>1 out of 1 records added</body></html>", true)]
    [InlineData("0 out of 1 records added. 1 duplicate", true)]
    [InlineData("0 out of 1 records added", false)]
    [InlineData("Error: Invalid password. 1 out of 1 records added", false)]
    [InlineData("<html>Sign in to eQSL</html>", false)]
    [InlineData("", false)]
    public void ParseResponse_AcknowledgmentRequired_RejectsUnknownReplies(string body, bool accepted)
    {
        EqslClient.ParseResponse(body).Accepted.Should().Be(accepted);
    }

    [Fact]
    public async Task Upload_CredentialsAndFt4_EncodedInBodyOnly()
    {
        string? body = null;
        using var handler = new Handler(async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri.Should().Be(EqslClient.DefaultEndpoint);
            request.Headers.UserAgent.ToString().Should().StartWith("Costas/");
            body = WebUtility.UrlDecode(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("1 out of 1 records added") };
        });
        using var http = HttpFactory.Create("W7LIT", handler);
        var record = AdifMapper.ToAdif(Contact("home", "W7LIT"));
        record["mode"] = "MFSK";
        record["submode"] = "FT4";
        var result = await new EqslClient(http).UploadAsync("W7LIT", "a&b+<c", "Home & Away", record, TestContext.Current.CancellationToken);
        result.Accepted.Should().BeTrue();
        body.Should().StartWith("ADIFData=").And.Contain("<eqsl_user:5>W7LIT").And.Contain("<eqsl_pswd:6>a&b+<c")
            .And.Contain("<eqsl_qth_nickname:11>Home & Away").And.Contain("<mode:4>MFSK").And.Contain("<submode:3>FT4");
        body!.IndexOf("<eqsl_pswd", StringComparison.Ordinal).Should().BeLessThan(body.IndexOf("<eoh>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Upload_ServerEchoesCredentials_DiagnosticContainsNoResponseData()
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("Error: invalid password a%26b%2B%3Cc for W7LIT") }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var result = await new EqslClient(http).UploadAsync("W7LIT", "a&b+<c", "", new AdifRecord(), TestContext.Current.CancellationToken);
        result.Accepted.Should().BeFalse();
        result.Error.Should().NotContainAny("W7LIT", "a%26b", "a&b");
    }

    [Fact]
    public async Task Upload_CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var handler = new Handler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct));
        using var http = HttpFactory.Create("W7LIT", handler);
        var act = () => new EqslClient(http).UploadAsync("W7LIT", "secret", "", new AdifRecord(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Queue_RetryThenSuccess_KeepsOtherTargetAndProfilesIndependent()
    {
        var db = SqliteDatabase.InMemory(out var keepAlive);
        using var connection = keepAlive;
        var qsos = new QsoRepository(db);
        var queue = new UploadQueueRepository(db);
        var clock = new ManualClock(Now);
        var id = qsos.Insert(Contact("home", "W7LIT"));
        var other = qsos.Insert(Contact("portable", "W7LIT/P"));
        queue.Enqueue(id, "qrz", Now);
        queue.Enqueue(id, "eqsl", Now);
        queue.Enqueue(other, "eqsl", Now);
        var reply = "Error: invalid password";
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply) }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var worker = new EqslUploadService(new EqslClient(http), qsos, queue, clock, (wait, _) => { clock.Advance(wait); return Task.CompletedTask; });
        await worker.ProcessDueAsync("home", "W7LIT", "W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        queue.All().Single(i => i.QsoId == id && i.Target == "eqsl").Attempts.Should().Be(1);
        qsos.Get(id)!.UploadState.Should().Be(UploadState.Queued);
        qsos.Get(id)!.EqslUploadState.Should().Be(UploadState.Queued);
        // A new worker sees the persisted pending item after a restart.
        clock.Advance(TimeSpan.FromMinutes(10));
        reply = "1 out of 1 records added";
        var restarted = new EqslUploadService(new EqslClient(http), qsos, new UploadQueueRepository(db), clock);
        (await restarted.ProcessDueAsync("home", "W7LIT", "W7LIT", "secret", "Home", TestContext.Current.CancellationToken)).Should().Be(1);
        qsos.Get(id)!.EqslUploadState.Should().Be(UploadState.Uploaded);
        qsos.Get(id)!.UploadState.Should().Be(UploadState.Queued);
        queue.All().Should().HaveCount(2);
        queue.All().Should().Contain(i => i.QsoId == other && i.Target == "eqsl");
        qsos.SetUploadState(id, UploadState.Uploaded, null, 1234);
        qsos.Get(id)!.EqslUploadState.Should().Be(UploadState.Uploaded);
    }

    [Theory]
    [InlineData("Y", true)]
    [InlineData("V", true)]
    [InlineData("N", false)]
    public void Adif_EqslConfirmation_Recognized(string value, bool expected)
    {
        AdifMapper.IsConfirmed(new AdifRecord { ["eqsl_qsl_rcvd"] = value }).Should().Be(expected);
    }

    [Fact]
    public async Task Inbox_DownloadLink_FetchesOnlySameOriginAdif()
    {
        var requests = new List<Uri>();
        using var handler = new Handler(async (request, ct) =>
        {
            requests.Add(request.RequestUri!);
            if (request.Method == HttpMethod.Post)
            {
                var form = WebUtility.UrlDecode(await request.Content!.ReadAsStringAsync(ct));
                form.Should().Contain("UserName=W7LIT").And.Contain("Password=secret").And.Contain("QTHNickname=Home");
                request.RequestUri!.Query.Should().BeEmpty();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<a href='https://evil.example/cards.adi'>bad</a><a href='downloadedfiles/inbox.adi'>Download</a>") };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AdifWriter.Document([AdifMapper.ToAdif(Contact("home", "W7LIT"))], Now)) };
        });
        using var http = HttpFactory.Create("W7LIT", handler);
        var cards = await new EqslClient(http).DownloadInboxAsync("W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        cards.Should().HaveCount(1);
        requests.Should().HaveCount(2);
        requests[1].AbsoluteUri.Should().Be("https://www.eqsl.cc/qslcard/downloadedfiles/inbox.adi");
    }

    [Fact]
    public async Task Inbox_DownloadedFilesAboveQslcard_FollowsLink()
    {
        // Since October 2019 eQSL links the file one folder up, outside /qslcard/.
        var requests = new List<Uri>();
        using var handler = new Handler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            var reply = request.Method == HttpMethod.Post
                ? "<H3>Your ADIF log file has been built</H3><A HREF=\"../Graphics/logo.gif\">x</A><LI><A HREF=\"../downloadedfiles/W7LIT1.adi\">.ADI file</A>"
                : AdifWriter.Document([AdifMapper.ToAdif(Contact("home", "W7LIT"))], Now);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply) });
        });
        using var http = HttpFactory.Create("W7LIT", handler);
        var cards = await new EqslClient(http).DownloadInboxAsync("W7LIT", "secret", "", TestContext.Current.CancellationToken);
        cards.Should().HaveCount(1);
        requests[1].AbsoluteUri.Should().Be("https://www.eqsl.cc/downloadedfiles/W7LIT1.adi");
    }

    [Theory]
    [InlineData("http://www.eqsl.cc/downloadedfiles/a.adi", "https://www.eqsl.cc/downloadedfiles/a.adi")]
    [InlineData("https://eqsl.cc/DownloadedFiles/a.adi", "https://eqsl.cc/DownloadedFiles/a.adi")]
    public async Task Inbox_AbsoluteEqslLink_FetchedOverHttps(string href, string expected)
    {
        var requests = new List<Uri>();
        using var handler = new Handler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            var reply = request.Method == HttpMethod.Post ? $"<a href='{href}'>.ADI file</a>" : "<eoh>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply) });
        });
        using var http = HttpFactory.Create("W7LIT", handler);
        await new EqslClient(http).DownloadInboxAsync("W7LIT", "secret", "", TestContext.Current.CancellationToken);
        requests.Should().HaveCount(2);
        requests[1].AbsoluteUri.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://www.eqsl.cc/downloadedfiles/a.txt")]
    [InlineData("https://www.eqsl.cc/Graphics/a.adi")]
    [InlineData("https://www.eqsl.cc:8443/downloadedfiles/a.adi")]
    [InlineData("http://evil.example/downloadedfiles/a.adi")]
    [InlineData("https://www.eqsl.cc/downloadedfiles/a.adi?x=1")]
    public async Task Inbox_OtherLinks_NotFollowed(string href)
    {
        var requests = 0;
        using var handler = new Handler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"<a href='{href}'>file</a>") });
        });
        using var http = HttpFactory.Create("W7LIT", handler);
        var act = () => new EqslClient(http).DownloadInboxAsync("W7LIT", "secret", "", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<EqslException>();
        requests.Should().Be(1);
    }

    [Fact]
    public async Task Inbox_NoLogEntries_ReturnsEmpty()
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("<DIV><H3>You have no log entries</H3></DIV>") }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var cards = await new EqslClient(http).DownloadInboxAsync("W7LIT", "secret", "", TestContext.Current.CancellationToken);
        cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Inbox_WrongPassword_QuotesEqslWithoutCredentials()
    {
        // Captured from eQSL on 2026-10-31 with a wrong password.
        const string reply = "<DIV ALIGN=\"CENTER\">\n        <H3>Error: No such Username/Password found</H3>\n"
            + "        This could mean the wrong callsign or the wrong password, or the user does not exist.\n        </BODY>";
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply) }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var act = () => new EqslClient(http).DownloadInboxAsync("W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        (await act.Should().ThrowAsync<EqslException>()).Which.Message.Should()
            .Be("eQSL said \"Error: No such Username/Password found This could mean the wrong callsign or the wrong password, or the user does not exist.\". "
                + "Check the username (your eQSL callsign) and password in Settings.");
    }

    [Theory]
    [InlineData("a&b+<c")]
    [InlineData("pa<ss>word")]
    [InlineData("x&amp;y")]
    public void ServerMessage_EchoedPassword_Blanked(string password)
    {
        var echoes = new[] { password, WebUtility.UrlEncode(password), WebUtility.HtmlEncode(password) };
        foreach (var echo in echoes)
        {
            var said = EqslClient.ServerMessage($"<h3>Error: bad password {echo} for <eqsl_pswd:{password.Length}>{password}</h3>", "W7LIT", password, "");
            said.Should().StartWith("Error: bad password").And.NotContain(password).And.NotContain("W7LIT");
            said.Should().NotContain(WebUtility.UrlEncode(password)).And.NotContain(WebUtility.HtmlEncode(password));
        }
    }

    [Fact]
    public void ParseResponse_Refused_QuotesEqsl()
    {
        var result = EqslClient.ParseResponse("<b>Error: No match on eQSL_User/eQSL_Pswd</b>", "W7LIT", "secret", "Home");
        result.Accepted.Should().BeFalse();
        result.Error.Should().Be("eQSL said \"Error: No match on eQSL_User/eQSL_Pswd\". Check the username (your eQSL callsign) and password in Settings.");
    }

    [Fact]
    public async Task Inbox_LoginHtml_RefusedWithoutAdvancingSyncState()
    {
        var db = SqliteDatabase.InMemory(out var keepAlive);
        using var connection = keepAlive;
        var state = new SyncStateRepository(db);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Error: password secret") }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var worker = new EqslSyncService(new EqslClient(http), new QsoRepository(db), new EqslInboxRepository(db), state, new ManualClock(Now));
        var act = () => worker.SyncAsync("home", "W7LIT", "W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
        state.Get("home", "eqsl").LastSyncUtc.Should().BeNull();
    }

    [Fact]
    public async Task Inbox_Ft4AndUnmatchedCards_PersistsAndConfirmsWithoutOverwritingQrz()
    {
        var db = SqliteDatabase.InMemory(out var keepAlive);
        using var connection = keepAlive;
        var qsos = new QsoRepository(db);
        var q = Contact("home", "W7LIT");
        q.Mode = "MFSK"; q.Submode = "FT4"; q.QsoDateOn = Now.AddSeconds(30);
        q.UploadState = UploadState.Uploaded; q.QrzLogid = 123;
        var id = qsos.Insert(q);
        var other = Contact("portable", "W7LIT/P");
        var otherId = qsos.Insert(other);
        var card = AdifMapper.ToAdif(Contact("home", "W7LIT"));
        card["mode"] = "FT4"; card["time_on"] = "0300";
        var unmatched = AdifMapper.ToAdif(Contact("home", "W7LIT"));
        unmatched["call"] = "K2ABC";
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(AdifWriter.Document([card, unmatched], Now)) }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var inbox = new EqslInboxRepository(db);
        var state = new SyncStateRepository(db);
        var worker = new EqslSyncService(new EqslClient(http), qsos, inbox, state, new ManualClock(Now));
        var result = await worker.SyncAsync("home", "W7LIT", "W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        result.Should().Be(new EqslSyncResult(2, 1, 1));
        qsos.Get(id)!.EqslConfirmed.Should().BeTrue();
        qsos.Get(id)!.QrzLogid.Should().Be(123);
        qsos.Get(otherId)!.EqslConfirmed.Should().BeFalse();
        inbox.Read("home").Should().HaveCount(2);
        state.Get("home", "eqsl").LastSyncUtc.Should().Be(Now);
        var second = await worker.SyncAsync("home", "W7LIT", "W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        second.Confirmed.Should().Be(0);
        inbox.Read("home").Should().HaveCount(2);
        // QRZ's next refresh must not clear a confirmation received from eQSL.
        var fromQrz = qsos.Get(id)!.Clone();
        fromQrz.Source = QsoSource.Qrz; fromQrz.Confirmed = false; fromQrz.EqslConfirmed = false;
        qsos.Upsert(fromQrz);
        qsos.Get(id)!.Confirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Inbox_AmbiguousSameMinute_DoesNotConfirmEitherContact()
    {
        var db = SqliteDatabase.InMemory(out var keepAlive);
        using var connection = keepAlive;
        var qsos = new QsoRepository(db);
        var id = qsos.Insert(Contact("home", "W7LIT"));
        var second = Contact("home", "W7LIT"); second.QsoDateOn = Now.AddSeconds(40);
        var secondId = qsos.Insert(second);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(AdifWriter.Document([AdifMapper.ToAdif(Contact("home", "W7LIT"))], Now)) }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var worker = new EqslSyncService(new EqslClient(http), qsos, new EqslInboxRepository(db), new SyncStateRepository(db), new ManualClock(Now));
        var result = await worker.SyncAsync("home", "W7LIT", "W7LIT", "secret", "Home", TestContext.Current.CancellationToken);
        result.Unmatched.Should().Be(1);
        qsos.Get(id)!.Confirmed.Should().BeFalse();
        qsos.Get(secondId)!.Confirmed.Should().BeFalse();
    }

    private static QsoRecord Contact(string profile, string station) => new()
    {
        ProfileId = profile, StationCallsign = station, Call = "K1ABC", Band = "20m", Mode = "FT8", QsoDateOn = Now,
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
