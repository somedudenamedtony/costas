// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Text;
using Ft8Client.Integrations.HamQth;

namespace Ft8Client.Integrations.Tests;

/// <summary>The HamQTH v2 client against a stub server answering in the shape https://www.hamqth.com/developers documents.</summary>
public class HamQthTests
{
    private const string Record = """
        {"callsign":"OK2CQR","nick":"Petr","name":"Petr Hlozek","qth":"Neratovice","dxcc":503,"country":"Czech Republic",
         "continent":"EU","cq":15,"itu":28,"utcOffset":1.0,"location":{"lat":50.253446,"lon":14.515041,"grid":"JO70GG"},
         "address":{"name":"Petr Hlozek","street":["x"],"city":"Neratovice","zip":"27711","country":"Czech Republic","dxcc":503},
         "region":{"district":"BME","usState":null,"usCounty":null,"oblast":null,"dok":null},"iota":null,
         "qsl":{"via":null,"bureau":true,"direct":true,"lotw":true,"eqsl":false},
         "picture":"https://www.hamqth.com/userfiles/x.jpg","profile":"https://www.hamqth.com/ok2cqr"}
        """;

    private sealed class Stub : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = [];
        public readonly List<string> Bodies = [];
        public int Logins;
        public Func<HttpRequestMessage, HttpResponseMessage>? Lookup;
        public HttpStatusCode LoginStatus = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            if (request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
            {
                Logins++;
                return LoginStatus == HttpStatusCode.OK
                    ? Json($$"""{"token":"t{{Logins}}","expiresIn":3600}""")
                    : new HttpResponseMessage(LoginStatus);
            }
            return Lookup!(request);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (HamQthClient Client, Stub Stub) Make()
    {
        var stub = new Stub();
        return (new HamQthClient(new HttpClient(stub), () => ("W7LIT", "secret")), stub);
    }

    [Fact]
    public async Task Lookup_Found_MapsTheDocumentedFields()
    {
        var (client, stub) = Make();
        stub.Lookup = _ => Json(Record);

        var r = (await client.LookupAsync("OK2CQR", CancellationToken.None))!;

        r.Name.Should().Be("Petr Hlozek");
        r.Nickname.Should().Be("Petr");
        r.City.Should().Be("Neratovice");
        r.Country.Should().Be("Czech Republic");
        r.Grid.Should().Be("JO70GG");
        r.Lat.Should().BeApproximately(50.253446, 1e-6);
        r.Dxcc.Should().Be(503);
        r.CqZone.Should().Be(15);
        r.ItuZone.Should().Be(28);
        r.Lotw.Should().BeTrue();
        r.Eqsl.Should().BeFalse();
        r.State.Should().BeNull();
        r.ImageUrl.Should().StartWith("https://");
    }

    [Fact]
    public async Task Lookup_PasswordOnlyInPostBodyAndTokenAsBearer()
    {
        var (client, stub) = Make();
        stub.Lookup = _ => Json(Record);

        await client.LookupAsync("OK2CQR/P", CancellationToken.None);

        var login = stub.Requests[0];
        login.Method.Should().Be(HttpMethod.Post);
        login.RequestUri!.ToString().Should().Be("https://www.hamqth.com/api/v2/login");
        stub.Bodies[0].Should().Contain("\"password\":\"secret\"");
        var lookup = stub.Requests[1];
        lookup.RequestUri!.AbsoluteUri.Should().Be("https://www.hamqth.com/api/v2/callbook/OK2CQR%2FP?appname=Costas");
        lookup.Headers.Authorization!.ToString().Should().Be("Bearer t1");
        stub.Requests.Should().OnlyContain(r => !r.RequestUri!.ToString().Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lookup_TokenKeptForItsHour()
    {
        var (client, stub) = Make();
        stub.Lookup = _ => Json(Record);

        await client.LookupAsync("OK2CQR", CancellationToken.None);
        await client.LookupAsync("OK1RR", CancellationToken.None);

        stub.Logins.Should().Be(1);
    }

    [Fact]
    public async Task Lookup_NotFound_Null()
    {
        var (client, stub) = Make();
        stub.Lookup = _ => Json("""{"title":"Not Found","status":404,"detail":"Callsign ZZ9ZZZ not found."}""", HttpStatusCode.NotFound);

        (await client.LookupAsync("ZZ9ZZZ", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Lookup_TokenExpired_LogsInAgainOnce()
    {
        var (client, stub) = Make();
        var calls = 0;
        stub.Lookup = _ => ++calls == 1 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json(Record);

        var r = await client.LookupAsync("OK2CQR", CancellationToken.None);

        r.Should().NotBeNull();
        stub.Logins.Should().Be(2);
        stub.Requests.Last().Headers.Authorization!.Parameter.Should().Be("t2");
    }

    [Fact]
    public async Task Lookup_WrongPassword_ThrowsAuth()
    {
        var (client, stub) = Make();
        stub.LoginStatus = HttpStatusCode.Unauthorized;

        var act = () => client.LookupAsync("OK2CQR", CancellationToken.None);

        (await act.Should().ThrowAsync<HamQthException>()).Which.IsAuth.Should().BeTrue();
    }

    [Fact]
    public async Task Lookup_ServiceDown_ThrowsSoTheCallIsRetriedLater()
    {
        var (client, stub) = Make();
        stub.Lookup = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var act = () => client.LookupAsync("OK2CQR", CancellationToken.None);

        (await act.Should().ThrowAsync<HamQthException>()).Which.IsAuth.Should().BeFalse();
    }

    [Fact]
    public async Task Test_WrongPassword_False()
    {
        var (client, stub) = Make();
        stub.LoginStatus = HttpStatusCode.Unauthorized;

        (await client.TestAsync(CancellationToken.None)).Should().BeFalse();
    }
}
