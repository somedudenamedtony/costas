// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using Ft8Client.Core.Processes;

namespace Ft8Client.Integrations.Lotw;

/// <summary>
/// Signs and uploads an ADIF file through TQSL's command line (<c>tqsl -x -d -u -a compliant [-l location] file</c>):
/// batch mode, no date-range dialog, upload, and skip contacts LoTW already has. TQSL holds the callsign certificate;
/// Costas never sees it.
/// </summary>
public sealed class TqslUploader
{
    /// <summary>The longest a run may take before it is stopped.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly IProcessGuard _guard;

    /// <summary>Creates the uploader; <paramref name="guard"/> makes sure TQSL ends with the app.</summary>
    public TqslUploader(IProcessGuard guard) => _guard = guard;

    /// <summary>The usual places TQSL is installed, in order.</summary>
    public static IEnumerable<string> Candidates()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles") })
        {
            if (!string.IsNullOrEmpty(root)) yield return Path.Combine(root, "TrustedQSL", "tqsl.exe");
        }
        yield return "/usr/bin/tqsl";
        yield return "/usr/local/bin/tqsl";
        yield return "/Applications/TrustedQSL/tqsl.app/Contents/MacOS/tqsl";
    }

    /// <summary>The configured path if it exists, otherwise the first installed one, or null.</summary>
    public static string? Find(string? configured) =>
        !string.IsNullOrWhiteSpace(configured) ? (File.Exists(configured) ? configured : null) : Candidates().FirstOrDefault(File.Exists);

    /// <summary>The arguments for one run.</summary>
    public static IReadOnlyList<string> Arguments(string adifPath, string? stationLocation)
    {
        var args = new List<string> { "-x", "-d", "-u", "-a", "compliant" };
        if (!string.IsNullOrWhiteSpace(stationLocation))
        {
            args.Add("-l");
            args.Add(stationLocation);
        }
        args.Add(adifPath);
        return args;
    }

    /// <summary>Runs TQSL on <paramref name="adifPath"/> and waits for it.</summary>
    public async Task<TqslResult> UploadAsync(string tqslPath, string adifPath, string? stationLocation, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(tqslPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var a in Arguments(adifPath, stationLocation)) psi.ArgumentList.Add(a);
        Process? p;
        try
        {
            p = Process.Start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new TqslResult(-1, $"Could not start TQSL: {ex.Message}");
        }
        if (p is null) return new TqslResult(-1, "Could not start TQSL");
        using (p)
        {
            _guard.Adopt(p);
            var stderr = p.StandardError.ReadToEndAsync(ct);
            _ = p.StandardOutput.ReadToEndAsync(ct);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            try
            {
                await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
                return new TqslResult(-1, ct.IsCancellationRequested ? "TQSL was stopped" : "TQSL took too long and was stopped");
            }
            var code = p.ExitCode;
            var detail = LastLine(await stderr.ConfigureAwait(false));
            var msg = TqslResult.Describe(code);
            return new TqslResult(code, code is 0 or 8 or 9 || detail.Length == 0 ? msg : $"{msg}: {detail}");
        }
    }

    private static string LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty;
}
