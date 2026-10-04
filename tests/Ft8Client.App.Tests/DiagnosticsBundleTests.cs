// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.IO.Compression;
using Ft8Client.App.Services;

namespace Ft8Client.App.Tests;

public class DiagnosticsBundleTests
{
    [Fact]
    public void Write_SecretsInSettingsLogsAndRows_RemovedEverywhere()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ft8d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "logs"));
        try
        {
            const string key = "ABCD-1234-EF56-7890";
            const string password = "hunter2pass";
            File.WriteAllText(Path.Combine(dir, "settings.json"), $"{{\"stray\":\"{key}\"}}");
            File.WriteAllText(Path.Combine(dir, "logs", "app-1.log"), $"oops {password} and {key}\n");
            var zipPath = Path.Combine(dir, "bundle.zip");
            DiagnosticsBundle.Write(zipPath, Path.Combine(dir, "settings.json"), Path.Combine(dir, "logs"), [("Note", $"value {key}")], [key, password, "abc"]);

            using var zip = ZipFile.OpenRead(zipPath);
            zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo("settings.json", "logs/app-1.log", "diagnostics.txt");
            foreach (var e in zip.Entries)
            {
                using var r = new StreamReader(e.Open());
                var text = r.ReadToEnd();
                text.Should().NotContain(key).And.NotContain(password);
                text.Should().Contain("[removed]");
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
