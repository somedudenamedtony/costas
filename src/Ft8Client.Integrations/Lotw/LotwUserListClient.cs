// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Lotw;

/// <summary>Downloads ARRL's list of LoTW users and when each last uploaded (about 6 MB, refreshed by ARRL weekly).</summary>
public sealed class LotwUserListClient
{
    /// <summary>The list's address.</summary>
    public const string Url = "https://lotw.arrl.org/lotw-user-activity.csv";

    /// <summary>Timeout for the download.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;

    /// <summary>Creates a client on an <see cref="HttpClient"/> that identifies the app.</summary>
    public LotwUserListClient(HttpClient http) => _http = http;

    /// <summary>Downloads the list to <paramref name="path"/> (through a temporary file); false on any failure.</summary>
    public async Task<bool> DownloadAsync(string path, CancellationToken ct)
    {
        var tmp = path + ".part";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            using var response = await _http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await using (var file = File.Create(tmp))
            {
                await response.Content.CopyToAsync(file, cts.Token).ConfigureAwait(false);
            }
            if (new FileInfo(tmp).Length < 1000) return false;
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch (IOException)
            {
            }
        }
    }
}
