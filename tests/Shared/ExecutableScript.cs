// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;

namespace Ft8Client.Tests.Shared;

/// <summary>
/// Writes stand-in programs (a fake jt9, rigctld or secret-tool) that a test then runs. On Linux and macOS the file is
/// written by a separate shell, never opened for writing by the test process. Writing it here and running it at once
/// failed now and then with "Text file busy" (ETXTBSY): tests run in parallel, and a child process another test was
/// starting at that moment briefly held a copy of the still-open write handle.
/// </summary>
internal static class ExecutableScript
{
    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/> and makes it executable.</summary>
    public static void Write(string path, string content)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, content);
            return;
        }
        Shell("cat > \"$1\" && chmod 700 \"$1\"", content, path);
    }

    /// <summary>Copies <paramref name="source"/> to <paramref name="path"/> and makes the copy executable.</summary>
    public static void Copy(string source, string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.Copy(source, path);
            return;
        }
        Shell("cp \"$2\" \"$1\" && chmod 700 \"$1\"", null, path, source);
    }

    private static void Shell(string script, string? input, params string[] args)
    {
        var psi = new ProcessStartInfo("/bin/sh") { RedirectStandardInput = true, UseShellExecute = false };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("sh");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start /bin/sh");
        if (input is not null) p.StandardInput.Write(input);
        p.StandardInput.Close();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException($"Could not write {args[0]} (exit {p.ExitCode})");
    }
}
