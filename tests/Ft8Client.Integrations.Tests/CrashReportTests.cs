// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;
using FluentAssertions;
using Ft8Client.Integrations.Crash;
using Sentry;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Ft8Client.Integrations.Tests;

// One Sentry hub per process: these tests start and stop it, so they do not run beside each other.
[Collection(nameof(CrashReportTests))]
public class CrashReportTests
{
    private const string Dsn = "https://0123456789abcdef0123456789abcdef@o1.ingest.us.sentry.io/1";

    private sealed class CapturingTransport : ITransport
    {
        public List<string> Sent { get; } = [];

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Sent.Add(Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    private static CrashScrubber Scrubber() => new(["W7LIT", "DN40", "Tony Shepherd"], [@"C:\Users\tony", "/home/tony"]);

    [Theory]
    [InlineData("Could not log W7LIT", "Could not log [x]")]
    [InlineData("Answer to k1abc failed", "Answer to [x] failed")]
    [InlineData("Station VK2/G4ABC/P not found", "Station [x] not found")]
    [InlineData("Grid DN40ab is bad", "Grid [x] is bad")]
    [InlineData("Grid FN31 is bad", "Grid [x] is bad")]
    [InlineData("Dial 14074000 out of band", "Dial [x] out of band")]
    [InlineData("Dial 14.074 out of band", "Dial [x] out of band")]
    [InlineData("Dial 7.074.000 Hz", "Dial [x]")]
    [InlineData("Offset 1500 Hz", "Offset [x]")]
    [InlineData("Port COM3 is busy", "Port [x] is busy")]
    [InlineData("Port /dev/ttyUSB0 is busy", "Port [x] is busy")]
    [InlineData("Cannot reach 192.168.1.20:4532", "Cannot reach [x]")]
    [InlineData("Mail tony@example.com", "Mail [x]")]
    [InlineData("GET https://xmldata.qrz.com/xml/current/?s=abc123&callsign=W1AW", "GET https://xmldata.qrz.com/xml/current/?s=[x]&callsign=[x]")]
    [InlineData(@"Could not open C:\Users\tony\AppData\Local\Costas\costas.db", @"Could not open ~\AppData\Local\Costas\costas.db")]
    [InlineData(@"Could not open D:\Users\someone\x.adi", @"Could not open D:\Users\~\x.adi")]
    [InlineData("Could not open /home/someone/.local/share/Costas/x.adi", "Could not open /home/~/.local/share/Costas/x.adi")]
    [InlineData("Operator Tony Shepherd", "Operator [x]")]
    public void Scrub_PrivateValue_IsTakenOut(string text, string expected) => Scrubber().Scrub(text).Should().Be(expected);

    [Theory]
    [InlineData("Object reference not set to an instance of an object.")]
    [InlineData("Index was outside the bounds of the array.")]
    [InlineData("The operation has timed out.")]
    [InlineData("Value cannot be null. (Parameter 'source')")]
    [InlineData("Unable to load DLL 'portaudio' or one of its dependencies.")]
    [InlineData("X509Certificate chain failed")]
    [InlineData("Band 20m, 160m and 2m")]
    public void Scrub_OrdinaryError_IsKept(string text) => Scrubber().Scrub(text).Should().Be(text);

    [Fact]
    public void Scrub_Null_StaysNull() => Scrubber().Scrub(null).Should().BeNull();

    private static (SentryClient Client, CapturingTransport Transport) Client(Func<bool> allowed, string cache)
    {
        var transport = new CapturingTransport();
        var o = new SentryOptions();
        CrashReporter.Configure(o, Dsn, cache, allowed, () => ["W7LIT", "DN40"], () => true);
        o.CacheDirectoryPath = null; // straight to the transport in tests
        o.Transport = transport;
        return (new SentryClient(o), transport);
    }

    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message) { Data = { ["call"] = "W7LIT" } };
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public void Capture_Exception_SendsTypeAndStackWithoutPrivateValues()
    {
        var (client, transport) = Client(() => true, Path.GetTempPath());
        client.CaptureException(Thrown("Contact with W7LIT in DN40 on 14074000 Hz failed"));
        client.Flush(TimeSpan.FromSeconds(5));

        transport.Sent.Should().ContainSingle();
        var body = transport.Sent[0];
        body.Should().Contain("InvalidOperationException").And.Contain(nameof(Thrown));
        body.Should().NotContain("W7LIT").And.NotContain("DN40").And.NotContain("14074000");
        // The user folder, not the bare user name: that can also be an ordinary word in a module name ("runner" on CI).
        body.Should().NotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        body.Should().NotContain(Environment.MachineName);
        body.Should().NotContain("\"ip_address\"").And.NotContain("\"username\"").And.NotContain("\"server_name\"");
        body.Should().NotContain("\"breadcrumbs\"");
    }

    [Fact]
    public void Capture_ReportsTurnedOff_SendsNothing()
    {
        var on = true;
        var (client, transport) = Client(() => on, Path.GetTempPath());
        on = false;
        client.CaptureException(Thrown("boom"));
        client.Flush(TimeSpan.FromSeconds(5));
        transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public void Start_NoDsn_ReturnsNull() =>
        CrashReporter.Start(null, Path.GetTempPath(), () => true, () => []).Should().BeNull();

    [Fact]
    public void Start_TurnedOff_EmptiesTheCacheAndReturnsNull()
    {
        var folder = Path.Combine(Path.GetTempPath(), "costas-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "waiting.envelope"), "x");
        CrashReporter.Start(Dsn, folder, () => false, () => []).Should().BeNull();
        Directory.Exists(folder).Should().BeFalse();
    }

    [Fact]
    public void LogAndCount_Running_SendScrubbedLogAndCounter()
    {
        var transport = new CapturingTransport();
        var on = true;
        using (var reporter = CrashReporter.Start(Dsn, Path.GetTempPath(), () => on, () => ["W7LIT", "DN40"], () => true, o =>
               {
                   o.CacheDirectoryPath = null;
                   o.Transport = transport;
               }))
        {
            reporter.Should().NotBeNull();
            CrashReporter.Log(error: false, "Could not look up W7LIT in DN40 on 14074000 Hz {not a template}");
            CrashReporter.Count("band_changed", new Dictionary<string, object> { ["band"] = "20m", ["mode"] = "FT8" });
            on = false;
            CrashReporter.Log(error: true, "should not be sent");
            CrashReporter.Count("should_not_be_counted");
            on = true;
            Sentry.SentrySdk.Flush(TimeSpan.FromSeconds(5));
        }

        var all = string.Join("\n", transport.Sent);
        all.Should().Contain("Could not look up [x] in [x] on [x] {not a template}");
        all.Should().Contain("band_changed").And.Contain("20m");
        all.Should().NotContain("W7LIT").And.NotContain("14074000").And.NotContain("should not be sent").And.NotContain("should_not_be_counted");
        all.Should().NotContain(Environment.MachineName);
        all.Should().NotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        CrashReporter.Running.Should().BeFalse();
    }

    [Fact]
    public void Log_TwoRuns_CarryDifferentUserIds()
    {
        string RunOnce()
        {
            var transport = new CapturingTransport();
            using (CrashReporter.Start(Dsn, Path.GetTempPath(), () => true, () => [], () => true, o =>
                   {
                       o.CacheDirectoryPath = null;
                       o.Transport = transport;
                   }))
            {
                CrashReporter.Log(error: false, "hello");
                Sentry.SentrySdk.Flush(TimeSpan.FromSeconds(5));
            }
            var m = System.Text.RegularExpressions.Regex.Match(string.Join("\n", transport.Sent), "\"user.id\":\\{\"value\":\"([^\"]+)\"");
            m.Success.Should().BeTrue();
            return m.Groups[1].Value;
        }

        // Sentry's own installation id would be the same on every run, a tracking id; each run gets its own instead.
        RunOnce().Should().NotBe(RunOnce());
    }
}
