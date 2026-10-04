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
public sealed class SecretToolStore : ISecretStore
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
        var (code, output) = Run(null, "lookup", Attribute, name);
        return code == 0 ? output.TrimEnd('\n') : null;
    }

    /// <inheritdoc />
    public void Set(string name, string value)
    {
        var (code, _) = Run(value, "store", "--label", name, Attribute, name);
        if (code != 0) throw new InvalidOperationException("secret-tool could not store the secret.");
    }

    /// <inheritdoc />
    public void Delete(string name) => Run(null, "clear", Attribute, name);

    private static (int Code, string Output) Run(string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo("secret-tool")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("secret-tool did not start.");
        if (stdin is not null) p.StandardInput.Write(stdin);
        p.StandardInput.Close();
        var output = p.StandardOutput.ReadToEnd();
        if (!p.WaitForExit(10_000))
        {
            p.Kill(true);
            return (-1, string.Empty);
        }
        return (p.ExitCode, output);
    }
}
