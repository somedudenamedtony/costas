// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Ft8Client.Integrations.Updates;

/// <summary>
/// Asks GitHub for the latest Costas release. Unauthenticated (60 requests an hour per address; the app asks once a
/// day). A private or missing repository, no release, or no network all read as "no update", never as an error to show.
/// </summary>
public sealed class UpdateChecker(HttpClient http, string repository, Uri? apiBase = null)
{
    /// <summary>The GitHub repository releases come from.</summary>
    public const string DefaultRepository = "somedudenamedtony/costas";

    private readonly Uri _api = apiBase ?? new Uri("https://api.github.com/");

    /// <summary>
    /// The tag of the one development release: a GitHub prerelease that CI replaces with each green build of main. GitHub
    /// never marks a prerelease as latest, so release checks (and builds that know nothing of it) never see it.
    /// </summary>
    public const string DevTag = "dev";

    /// <summary>The latest release with a Costas installer, or null if there is none (or it cannot be reached).</summary>
    public Task<ReleaseInfo?> LatestAsync(CancellationToken ct) => FetchAsync("releases/latest", development: false, ct);

    /// <summary>The current development build, or null if there is none (or it cannot be reached).</summary>
    public Task<ReleaseInfo?> LatestDevelopmentAsync(CancellationToken ct) => FetchAsync($"releases/tags/{DevTag}", development: true, ct);

    private async Task<ReleaseInfo?> FetchAsync(string path, bool development, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(_api, $"repos/{repository}/{path}"));
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var release = Parse(json, development);
        if (release is null || release.Sha256 is not null) return release;
        // Older releases carry the checksum as a separate asset instead of GitHub's digest field.
        var shaUrl = ShaAssetUrl(json, release.InstallerName);
        return shaUrl is null ? release : release with { Sha256 = await ReadShaAsync(shaUrl, ct).ConfigureAwait(false) };
    }

    /// <summary>True when <paramref name="release"/> is newer than the running build.</summary>
    public static bool IsNewer(ReleaseInfo release, string runningVersion) =>
        Version.TryParse(runningVersion, out var running) && release.Version > running;

    /// <summary>
    /// Parses a GitHub release JSON object; null when it has no usable version or installer asset. A prerelease is read
    /// only as the development release (<paramref name="development"/>), whose version is in its title, not its tag.
    /// </summary>
    public static ReleaseInfo? Parse(string json, bool development = false)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        var prerelease = root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
        if (prerelease != development) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        Version? version;
        if (development)
        {
            var title = root.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            version = VersionIn(title);
            if (version is null) return null;
            tag = $"v{version}";
        }
        else if (!Version.TryParse(tag.TrimStart('v', 'V'), out version))
        {
            return null;
        }
        var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? string.Empty : string.Empty;
        ReleaseInfo? classic = null;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? string.Empty;
            var velopack = string.Equals(name, development ? VelopackDevSetupName : VelopackSetupName, StringComparison.OrdinalIgnoreCase);
            if (!velopack && !IsInstallerName(name)) continue;
            string? sha = null;
            if (a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String && d.GetString() is { } digest &&
                digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                sha = digest[7..].ToLowerInvariant();
            }
            var info = new ReleaseInfo(version, tag, page, name, a.GetProperty("browser_download_url").GetString() ?? string.Empty,
                a.TryGetProperty("size", out var s) ? s.GetInt64() : 0, sha) { IsDevelopment = development };
            // A release with the Velopack installer moves a classic install over to delta updates; the classic installer
            // stays in releases for builds that predate Velopack, which only know to look for it.
            if (velopack) return info;
            classic ??= info;
        }
        return classic;
    }

    /// <summary>
    /// The Velopack installer published with each release since the move to delta updates. A classic (Inno Setup) install
    /// runs it once to move to a Velopack install; from then on updates are deltas.
    /// </summary>
    public const string VelopackSetupName = "CostasApp-win-Setup.exe";

    /// <summary>The Velopack installer of a development build (Velopack's <c>dev</c> channel).</summary>
    public const string VelopackDevSetupName = "CostasApp-dev-Setup.exe";

    // The development release's title, "Costas 0.1.300 (development build)", carries its version.
    private static Version? VersionIn(string title)
    {
        foreach (var word in title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Contains('.', StringComparison.Ordinal) && Version.TryParse(word.TrimStart('v', 'V'), out var v)) return v;
        }
        return null;
    }

    private static bool IsInstallerName(string name) =>
        name.StartsWith("Costas-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static string? ShaAssetUrl(string json, string installerName)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (string.Equals(a.GetProperty("name").GetString(), installerName + ".sha256", StringComparison.OrdinalIgnoreCase))
                return a.GetProperty("browser_download_url").GetString();
        }
        return null;
    }

    private async Task<string?> ReadShaAsync(string url, CancellationToken ct)
    {
        var text = (await http.GetStringAsync(new Uri(url), ct).ConfigureAwait(false)).Trim();
        var hex = text.Split(' ', '\t', '\n')[0].ToLowerInvariant();
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex : null;
    }

    /// <summary>For logs and the About window.</summary>
    public static string Describe(ReleaseInfo r) => string.Create(CultureInfo.InvariantCulture, $"{r.Tag} ({r.InstallerSize / 1_048_576.0:0} MB)");
}
