// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.Data.Secrets;
using Xunit;

namespace Ft8Client.Data.Tests;

// secret-tool stand-ins are shell scripts, so these run on Linux (and macOS) only.
public sealed class SecretToolStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "costas-secret-tool-" + Guid.NewGuid().ToString("N"));

    public SecretToolStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Script(string body)
    {
        var path = Path.Combine(_dir, "secret-tool");
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    [Fact]
    public void Set_NoKeyringRunning_ThrowsWithTheKeyringsReason()
    {
        if (OperatingSystem.IsWindows()) return;
        var store = new SecretToolStore(Script("cat >/dev/null; echo 'secret-tool: The name org.freedesktop.secrets was not provided by any .service files' >&2; exit 1"));

        var act = () => store.Set("Costas:test", "AAAA-BBBB");

        act.Should().Throw<InvalidOperationException>().WithMessage("The name org.freedesktop.secrets was not provided by any .service files");
    }

    [Fact]
    public void Get_StoredSecret_ReturnsItWithoutTheNewline()
    {
        if (OperatingSystem.IsWindows()) return;
        var store = new SecretToolStore(Script("echo AAAA-BBBB"));

        store.Get("Costas:test").Should().Be("AAAA-BBBB");
    }

    [Fact]
    public void Set_SecretOnStandardInput_ReachesSecretToolAndNotItsArguments()
    {
        if (OperatingSystem.IsWindows()) return;
        var got = Path.Combine(_dir, "got.txt");
        var store = new SecretToolStore(Script($"cat > '{got}'; echo \"$@\" >> '{got}'"));

        store.Set("Costas:test", "AAAA-BBBB");

        File.ReadAllText(got).Should().Be("AAAA-BBBBstore --label Costas:test ft8client Costas:test\n");
    }
}
