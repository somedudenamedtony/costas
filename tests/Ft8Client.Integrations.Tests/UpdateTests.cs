// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ft8Client.Integrations.Updates;

namespace Ft8Client.Integrations.Tests;

public class UpdateTests
{
    private static readonly byte[] Installer = Encoding.ASCII.GetBytes("pretend installer bytes");
    private static readonly string InstallerSha = Convert.ToHexStringLower(SHA256.HashData(Installer));

    private static string Release(string tag, bool digest = true, bool shaAsset = false, bool prerelease = false) => $$"""
        {
          "tag_name": "{{tag}}", "draft": false, "prerelease": {{(prerelease ? "true" : "false")}},
          "html_url": "https://github.com/somedudenamedtony/costas/releases/tag/{{tag}}",
          "assets": [
            { "name": "notes.txt", "size": 3, "browser_download_url": "https://example.invalid/notes.txt" },
            { "name": "Costas-Setup-{{tag[1..]}}.exe", "size": {{Installer.Length}},
              "browser_download_url": "https://example.invalid/Costas-Setup.exe"{{(digest ? $", \"digest\": \"sha256:{InstallerSha.ToUpperInvariant()}\"" : string.Empty)}} }
            {{(shaAsset ? $", {{ \"name\": \"Costas-Setup-{tag[1..]}.exe.sha256\", \"size\": 64, \"browser_download_url\": \"https://example.invalid/sha\" }}" : string.Empty)}}
          ]
        }
        """;

    private sealed class FakeGitHub(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public void Parse_ReleaseWithDigest_ReadsVersionInstallerAndChecksum()
    {
        var r = UpdateChecker.Parse(Release("v0.1.0.42"))!;
        r.Version.Should().Be(new Version(0, 1, 0, 42));
        r.InstallerName.Should().Be("Costas-Setup-0.1.0.42.exe");
        r.Sha256.Should().Be(InstallerSha);
        r.PageUrl.Should().StartWith("https://github.com/");
    }

    [Fact]
    public void Parse_PrereleaseOrNoInstaller_Null()
    {
        UpdateChecker.Parse(Release("v0.1.0.42", prerelease: true)).Should().BeNull();
        UpdateChecker.Parse("""{ "tag_name": "v1.0", "assets": [] }""").Should().BeNull();
        UpdateChecker.Parse("""{ "tag_name": "nightly", "assets": [] }""").Should().BeNull();
    }

    [Theory]
    [InlineData("0.1.0", true)]
    [InlineData("0.1.0.41", true)]
    [InlineData("0.1.0.42", false)]
    [InlineData("0.1.0.50", false)]
    [InlineData("0.2.0", false)]
    public void IsNewer_RunningVersion_ComparesNumerically(string running, bool newer) =>
        UpdateChecker.IsNewer(UpdateChecker.Parse(Release("v0.1.0.42"))!, running).Should().Be(newer);

    [Fact]
    public async Task LatestAsync_PrivateOrMissingRepo_NullNotError()
    {
        using var http = new HttpClient(new FakeGitHub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        (await new UpdateChecker(http, "x/y").LatestAsync(TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task LatestAsync_NoDigestButShaAsset_ReadsChecksumFromAsset()
    {
        var fake = new FakeGitHub(u => u.AbsolutePath.EndsWith("/sha", StringComparison.Ordinal)
            ? Ok(InstallerSha + "  Costas-Setup-0.1.0.42.exe\n")
            : Ok(Release("v0.1.0.42", digest: false, shaAsset: true)));
        using var http = new HttpClient(fake);
        var r = await new UpdateChecker(http, "somedudenamedtony/costas").LatestAsync(TestContext.Current.CancellationToken);
        r!.Sha256.Should().Be(InstallerSha);
        fake.Requests[0].ToString().Should().Be("https://api.github.com/repos/somedudenamedtony/costas/releases/latest");
    }

    [Fact]
    public async Task DownloadAsync_MatchingChecksum_SavesInstaller()
    {
        var dir = Directory.CreateTempSubdirectory("costas-upd").FullName;
        try
        {
            using var http = new HttpClient(new FakeGitHub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer) }));
            var path = await new UpdateDownloader(http).DownloadAsync(UpdateChecker.Parse(Release("v0.1.0.42"))!, dir, null, TestContext.Current.CancellationToken);
            File.ReadAllBytes(path).Should().Equal(Installer);
            Path.GetFileName(path).Should().Be("Costas-Setup-0.1.0.42.exe");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_TamperedFile_RefusedAndNothingLeft()
    {
        var dir = Directory.CreateTempSubdirectory("costas-upd").FullName;
        try
        {
            using var http = new HttpClient(new FakeGitHub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }));
            var act = () => new UpdateDownloader(http).DownloadAsync(UpdateChecker.Parse(Release("v0.1.0.42"))!, dir, null, TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<InvalidDataException>();
            Directory.EnumerateFiles(dir).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_NoChecksum_RefusedBeforeDownloading()
    {
        var fake = new FakeGitHub(_ => Ok("x"));
        using var http = new HttpClient(fake);
        var act = () => new UpdateDownloader(http).DownloadAsync(UpdateChecker.Parse(Release("v0.1.0.42", digest: false))!, Path.GetTempPath(), null,
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidDataException>();
        fake.Requests.Should().BeEmpty();
    }
}
