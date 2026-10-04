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

    /// <summary>The latest release with a Costas installer, or null if there is none (or it cannot be reached).</summary>
    public async Task<ReleaseInfo?> LatestAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(_api, $"repos/{repository}/releases/latest"));
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var release = Parse(json);
        if (release is null || release.Sha256 is not null) return release;
        // Older releases carry the checksum as a separate asset instead of GitHub's digest field.
        var shaUrl = ShaAssetUrl(json, release.InstallerName);
        return shaUrl is null ? release : release with { Sha256 = await ReadShaAsync(shaUrl, ct).ConfigureAwait(false) };
    }

    /// <summary>True when <paramref name="release"/> is newer than the running build.</summary>
    public static bool IsNewer(ReleaseInfo release, string runningVersion) =>
        Version.TryParse(runningVersion, out var running) && release.Version > running;

    /// <summary>Parses a GitHub release JSON object; null when it has no usable tag or installer asset.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
        var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? string.Empty : string.Empty;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? string.Empty;
            if (!IsInstallerName(name)) continue;
            string? sha = null;
            if (a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String && d.GetString() is { } digest &&
                digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                sha = digest[7..].ToLowerInvariant();
            }
            return new ReleaseInfo(version, tag, page, name, a.GetProperty("browser_download_url").GetString() ?? string.Empty,
                a.TryGetProperty("size", out var s) ? s.GetInt64() : 0, sha);
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
