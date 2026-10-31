// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;

namespace Ft8Client.Data.Secrets;

/// <summary>
/// The freedesktop Secret Service through <c>secret-tool</c> (libsecret). The secret is passed on standard input,
/// never on the command line.
/// </summary>
public sealed class SecretToolStore(string program = "secret-tool") : ISecretStore
{
    private const string Attribute = "ft8client";

    /// <summary>True when <c>secret-tool</c> is installed.</summary>
    public static bool IsAvailable() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator)
            .Any(p => File.Exists(Path.Combine(p, "secret-tool")));

    /// <inheritdoc />
    public bool IsPersistent => true;

    /// <inheritdoc />
    public string? Get(string name)
    {
        var (code, output, _) = Run(null, "lookup", Attribute, name);
        return code == 0 ? output.TrimEnd('\n') : null;
    }

    /// <inheritdoc />
    public void Set(string name, string value)
    {
        var (code, _, error) = Run(value, "store", "--label", name, Attribute, name);
        if (code == 0) return;
        // The keyring's own reason, for example "The name org.freedesktop.secrets was not provided by any .service
        // files" on a desktop with no keyring running, so the operator can see what to fix.
        var why = error.Trim().Replace("secret-tool: ", string.Empty, StringComparison.Ordinal);
        throw new InvalidOperationException(code == TimedOut ? "the keyring did not answer within 2 minutes"
            : why.Length > 0 ? why : "secret-tool could not store it");
    }

    /// <inheritdoc />
    public void Delete(string name) => Run(null, "clear", Attribute, name);

    // A locked keyring shows an unlock (or new keyring) prompt and secret-tool waits for it, so the operator gets time
    // to type the password. The wait has an end: before, stdout was read to the end first, so the timeout never fired
    // and an unanswered prompt froze Costas for good.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    private const int TimedOut = -1;

    private (int Code, string Output, string Error) Run(string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo(program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("secret-tool did not start.");
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        if (stdin is not null) p.StandardInput.Write(stdin);
        p.StandardInput.Close();
        if (!p.WaitForExit(Timeout))
        {
            p.Kill(true);
            return (TimedOut, string.Empty, string.Empty);
        }
        p.WaitForExit(); // the end of output too
        return (p.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
