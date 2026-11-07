// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Integrations.Crash;
using Serilog.Core;
using Serilog.Events;

namespace Ft8Client.App.Services;

/// <summary>
/// Hands the app's warnings and errors to Sentry Logs: the rendered line and, when there is one, the exception's type
/// and text, all scrubbed by <see cref="CrashReporter"/>. Nothing goes out until crash reports have started, nor when
/// they are switched off. Information and below stay in the local log only.
/// </summary>
internal sealed class SentryLogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Warning) return;
        try
        {
            var text = logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture);
            if (logEvent.Exception is { } ex) text += $" ({ex.GetType().FullName}: {ex.Message})";
            CrashReporter.Log(logEvent.Level >= LogEventLevel.Error, text);
        }
        catch (Exception)
        {
            // Never let remote logging break local logging.
        }
    }
}
