// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Net.NetworkInformation;
using Ft8Client.Core;
using Sentry;
using Sentry.Protocol;

namespace Ft8Client.Integrations.Crash;

/// <summary>
/// Crash and error reports, warning and error log lines, and usage counts to Sentry. Follows the same rules as the usage statistics: on unless the operator turns
/// them off in Settings, Privacy (read for every report, so switching off takes effect at once), never in simulation,
/// nothing sent or tried while the computer is offline (reports wait in a small cache on disk and go out when the
/// network is back), and nothing that identifies the operator: no callsign, grid, name, frequencies, stations, log
/// data, user name, machine name or IP address. Breadcrumbs are off, because they would carry the app's log lines.
/// </summary>
public sealed class CrashReporter : IDisposable
{
    /// <summary>How many reports wait at most in the offline cache.</summary>
    public const int MaxCached = 30;

    private static Func<bool> s_allowed = () => false;
    private static Func<IEnumerable<string?>> s_privateWords = () => [];

    private readonly IDisposable _sdk;

    private CrashReporter(IDisposable sdk) => _sdk = sdk;

    /// <summary>True once a reporter has been started in this process (a build with a DSN, reports allowed).</summary>
    public static bool Running { get; private set; }

    /// <summary>
    /// Starts reporting, or returns null when this build has no DSN or reports are off. When reports are off at start
    /// the cache is emptied, so nothing saved earlier goes out later. <paramref name="allowed"/> is read for every
    /// report; <paramref name="privateWords"/> is read for every report too (the operator's callsigns, grids and name,
    /// which can change while Costas runs).
    /// </summary>
    public static CrashReporter? Start(string? dsn, string cacheFolder, Func<bool> allowed, Func<IEnumerable<string?>> privateWords,
        Func<bool>? online = null) => Start(dsn, cacheFolder, allowed, privateWords, online, adjust: null);

    /// <summary>As <see cref="Start(string?, string, Func{bool}, Func{IEnumerable{string?}}, Func{bool}?)"/>; tests adjust the options (a fake transport).</summary>
    internal static CrashReporter? Start(string? dsn, string cacheFolder, Func<bool> allowed, Func<IEnumerable<string?>> privateWords,
        Func<bool>? online, Action<SentryOptions>? adjust)
    {
        if (string.IsNullOrWhiteSpace(dsn)) return null;
        if (!allowed())
        {
            DeleteCache(cacheFolder);
            return null;
        }
        if (Running) return null;
        s_allowed = allowed;
        s_privateWords = privateWords;
        var sdk = SentrySdk.Init(o =>
        {
            Configure(o, dsn.Trim(), cacheFolder, allowed, privateWords, online ?? NetworkInterface.GetIsNetworkAvailable);
            adjust?.Invoke(o);
        });
        // Logs would otherwise carry Sentry's installation id, which stays the same across runs: a tracking id. A new
        // random id for each run of Costas keeps one run's lines together and nothing more, as the statistics do.
        SentrySdk.ConfigureScope(scope => scope.User = new SentryUser { Id = Guid.NewGuid().ToString("N") });
        Running = true;
        return new CrashReporter(sdk);
    }

    /// <summary>Reports an error that was caught (the app carried on). Does nothing when no reporter is running.</summary>
    public static void Capture(Exception ex)
    {
        if (Running) SentrySdk.CaptureException(ex);
    }

    /// <summary>
    /// Counts a usage event as a Sentry metric, beside the Aptabase statistics. The caller passes only what the
    /// statistics may carry (band, mode, feature names: never a callsign, grid, frequency or log data); string values
    /// are scrubbed anyway. Does nothing when no reporter is running or reports are off.
    /// </summary>
    public static void Count(string name, IReadOnlyDictionary<string, object>? props = null)
    {
        if (!Running || !s_allowed()) return;
        var scrub = Scrubber(s_privateWords());
        var attributes = (props ?? new Dictionary<string, object>())
            .Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value is string text ? scrub.Scrub(text) ?? string.Empty : kv.Value))
            .ToArray();
        SentrySdk.Metrics.EmitCounter(name, 1, attributes, null);
    }

    /// <summary>
    /// Sends one log line (a warning or an error from the app's log) to Sentry Logs, scrubbed like a crash report: the
    /// rendered text only, none of the log's properties. Does nothing when no reporter is running or reports are off.
    /// </summary>
    public static void Log(bool error, string message)
    {
        if (!Running || !s_allowed()) return;
        var text = Scrubber(s_privateWords()).Scrub(message) ?? string.Empty;
        // The text goes in as a parameter, so braces in it are never read as a template.
        if (error) SentrySdk.Logger.LogError("{0}", text);
        else SentrySdk.Logger.LogWarning("{0}", text);
    }

    /// <summary>The Sentry options Costas uses. Internal for tests.</summary>
    internal static void Configure(SentryOptions o, string dsn, string cacheFolder, Func<bool> allowed, Func<IEnumerable<string?>> privateWords,
        Func<bool> online)
    {
        o.Dsn = dsn;
        o.Release = $"costas@{AppInfo.Version}";
        o.Environment = "production";
        o.IsGlobalModeEnabled = true;
        o.SendDefaultPii = false;
        o.IsEnvironmentUser = false;
        o.ServerName = string.Empty;
        o.AutoSessionTracking = false;
        o.SendClientReports = false;
        // Logs and metrics are only what Costas hands over itself (Log and Count), never an automatic integration.
        o.EnableLogs = false;
        o.DisableSystemDiagnosticsMetricsIntegration();
        o.SetBeforeSendLog(log => allowed() ? log : null);
        o.SetBeforeSendMetric(metric => allowed() ? metric : null);
        o.TracesSampleRate = 0;
        o.MaxBreadcrumbs = 0;
        o.SetBeforeBreadcrumb(_ => null);
        o.CacheDirectoryPath = cacheFolder;
        o.MaxCacheItems = MaxCached;
        o.NetworkStatusListener = new NetworkStatus(online);
        o.DisableDiagnosticSourceIntegration();
        o.SetBeforeSend((e, _) => allowed() ? Clean(e, Scrubber(privateWords())) : null);
    }

    /// <summary>A scrubber for the operator's words and this computer's user folder, user name and machine name.</summary>
    internal static CrashScrubber Scrubber(IEnumerable<string?> privateWords)
    {
        string? home = null;
        try
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        catch (PlatformNotSupportedException)
        {
        }
        return new CrashScrubber(privateWords.Append(SafeName(() => Environment.UserName)).Append(SafeName(() => Environment.MachineName)), [home]);
    }

    private static string? SafeName(Func<string> read)
    {
        try
        {
            var name = read();
            // A one- or two-letter name would take out pieces of ordinary words; the user folder is replaced anyway.
            return name.Length >= 3 ? name : null;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Takes everything that could identify the operator out of a report: the scrubber goes over every text that can
    /// carry what the app was doing, and the fields Sentry fills in about the person and the computer are cleared.
    /// </summary>
    internal static SentryEvent Clean(SentryEvent e, CrashScrubber scrub)
    {
        e.User = new SentryUser();
        e.ServerName = null;
        e.Request = new SentryRequest();
        if (e.Message is { } m)
            e.Message = new SentryMessage { Message = scrub.Scrub(m.Message), Formatted = scrub.Scrub(m.Formatted) };
        e.SetExtras(e.Extra.ToDictionary(kv => kv.Key, kv => (object?)scrub.Scrub(kv.Value?.ToString())));
        foreach (var tag in e.Tags.ToList()) e.SetTag(tag.Key, scrub.Scrub(tag.Value) ?? string.Empty);
        foreach (var x in e.SentryExceptions ?? [])
        {
            x.Value = scrub.Scrub(x.Value);
            if (x.Mechanism is { } mech)
            {
                mech.Data.Clear();
                mech.Description = scrub.Scrub(mech.Description);
            }
            CleanFrames(x.Stacktrace, scrub);
        }
        foreach (var t in e.SentryThreads ?? [])
        {
            t.Name = scrub.Scrub(t.Name);
            CleanFrames(t.Stacktrace, scrub);
        }
        foreach (var image in e.DebugImages ?? [])
        {
            image.CodeFile = scrub.Scrub(image.CodeFile);
            image.DebugFile = scrub.Scrub(image.DebugFile);
        }
        e.Contexts.Device.Name = null;
        e.Contexts.Device.Timezone = null;
        return e;
    }

    private static void CleanFrames(SentryStackTrace? trace, CrashScrubber scrub)
    {
        foreach (var f in trace?.Frames ?? [])
        {
            f.AbsolutePath = scrub.Scrub(f.AbsolutePath);
            f.FileName = scrub.Scrub(f.FileName);
            f.Vars.Clear();
            f.ContextLine = null;
            f.PreContext.Clear();
            f.PostContext.Clear();
        }
    }

    private static void DeleteCache(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Sends what is waiting (briefly, and only when online) and stops reporting.</summary>
    public void Dispose()
    {
        if (!Running) return;
        Running = false;
        _sdk.Dispose();
    }
}
