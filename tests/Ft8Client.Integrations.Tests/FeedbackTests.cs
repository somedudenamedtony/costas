// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Ft8Client.Integrations.Feedback;
using Xunit;

namespace Ft8Client.Integrations.Tests;

public class FeedbackTests
{
    private static readonly Uri Endpoint = new("https://formspree.example/f/abc");

    private sealed class FakeRelay(HttpStatusCode status = HttpStatusCode.OK, bool fail = false) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (fail) throw new HttpRequestException("offline");
            Requests.Add((request, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status);
        }
    }

    private static Dictionary<string, string> Fields(string body) => JsonSerializer.Deserialize<Dictionary<string, string>>(body)!;

    [Fact]
    public async Task SendAsync_Taken_PostsMessageSubjectAndReplyTo()
    {
        var relay = new FakeRelay();
        using var http = new HttpClient(relay);
        var error = await new FeedbackClient(http, Endpoint).SendAsync(new FeedbackReport("Waterfall froze\nafter band change", "op@example.com", "Costas 1.0 · Windows", null), CancellationToken.None);

        error.Should().BeNull();
        var (req, body) = relay.Requests.Single();
        req.RequestUri.Should().Be(Endpoint);
        req.Headers.Accept.ToString().Should().Contain("application/json");
        var f = Fields(body);
        f["_subject"].Should().Be("Costas feedback: Waterfall froze");
        f["email"].Should().Be("op@example.com");
        f["message"].Should().Contain("after band change").And.Contain("Costas 1.0 · Windows");
    }

    [Fact]
    public async Task SendAsync_NoReplyAddress_SendsNoEmailField()
    {
        var relay = new FakeRelay();
        using var http = new HttpClient(relay);
        await new FeedbackClient(http, Endpoint).SendAsync(new FeedbackReport("Idea", null, null, null), CancellationToken.None);

        Fields(relay.Requests.Single().Body).Should().NotContainKey("email");
    }

    [Fact]
    public async Task SendAsync_Refused_ReturnsReason()
    {
        using var http = new HttpClient(new FakeRelay(HttpStatusCode.UnprocessableEntity));
        var error = await new FeedbackClient(http, Endpoint).SendAsync(new FeedbackReport("x", null, null, null), CancellationToken.None);

        error.Should().Contain("422");
    }

    [Fact]
    public async Task SendAsync_Offline_ReturnsReasonWithoutThrowing()
    {
        using var http = new HttpClient(new FakeRelay(fail: true));
        var error = await new FeedbackClient(http, Endpoint).SendAsync(new FeedbackReport("x", null, null, null), CancellationToken.None);

        error.Should().Contain("Could not reach");
    }

    [Theory]
    [InlineData("https://formspree.io/f/xyz", true)]
    [InlineData("http://formspree.io/f/xyz", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EndpointFrom_Url_OnlyHttps(string? url, bool ok) => (FeedbackClient.EndpointFrom(url) is not null).Should().Be(ok);

    [Fact]
    public void Text_NothingTicked_IsOnlyTheMessage()
    {
        new FeedbackReport("  It crashed  ", null, null, null).Text().Should().Be("It crashed");
    }

    [Fact]
    public void Text_LogLeftOut_HasNoLog()
    {
        var r = new FeedbackReport("m", null, "sys", "W7XXX logged");
        r.Text(includeLog: false).Should().NotContain("W7XXX");
        r.Text().Should().Contain("--- Recent log ---").And.Contain("W7XXX logged");
    }

    [Fact]
    public void Subject_LongFirstLine_IsShortened()
    {
        new FeedbackReport(new string('a', 100), null, null, null).Subject.Length.Should().BeLessThan(80);
        new FeedbackReport("   ", null, null, null).Subject.Should().Be("Costas feedback");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("op@example.com", true)]
    [InlineData("op@example", false)]
    [InlineData("op example@x.com", false)]
    [InlineData("@example.com", false)]
    [InlineData("a@b@c.com", false)]
    public void IsReplyAddressValid_Address_Result(string address, bool ok) => FeedbackReport.IsReplyAddressValid(address).Should().Be(ok);

    [Fact]
    public void Link_Short_HasSubjectAndBodyAndNoLog()
    {
        var link = FeedbackMail.Link("tony@example.com", new FeedbackReport("Hello there", null, null, "secret log"));
        link.Should().StartWith("mailto:tony@example.com?subject=Costas%20feedback%3A%20Hello%20there&body=Hello%20there");
        link.Should().NotContain("secret");
    }

    [Fact]
    public void Link_Long_IsCutAndSaysSo()
    {
        var link = FeedbackMail.Link("tony@example.com", new FeedbackReport(string.Concat(Enumerable.Repeat("long text é ", 400)), null, null, null));
        link.Length.Should().BeLessThanOrEqualTo(FeedbackMail.MaxLinkLength);
        Uri.UnescapeDataString(link).Should().Contain("[Shortened");
    }
}
