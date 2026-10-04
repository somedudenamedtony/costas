// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Security.Cryptography;

namespace Ft8Client.Integrations.Updates;

/// <summary>Downloads a release's installer and checks it against the published SHA-256 before anything runs it.</summary>
public sealed class UpdateDownloader(HttpClient http)
{
    /// <summary>
    /// Downloads to <paramref name="folder"/> and returns the installer path. Throws <see cref="InvalidDataException"/> when
    /// the release publishes no checksum or the file does not match it; a file that fails the check is deleted.
    /// </summary>
    public async Task<string> DownloadAsync(ReleaseInfo release, string folder, IProgress<double>? progress, CancellationToken ct)
    {
        if (release.Sha256 is null) throw new InvalidDataException("This release has no checksum, so the download cannot be verified.");
        if (release.InstallerName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || release.InstallerName.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("The installer name is not a plain file name.");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, release.InstallerName);
        var partial = path + ".partial";
        try
        {
            using (var resp = await http.GetAsync(new Uri(release.InstallerUrl), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? release.InstallerSize;
                await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long done = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    if (total > 0) progress?.Report((double)done / total);
                }
            }
            string actual;
            await using (var f = File.OpenRead(partial))
            {
                actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(f, ct).ConfigureAwait(false));
            }
            if (!string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded installer does not match the published checksum.");
            File.Move(partial, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
