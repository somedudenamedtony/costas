// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;

namespace Ft8Client.Integrations.Feedback;

/// <summary>
/// One problem report or piece of feedback, exactly as the operator chose to send it. The system line and the log are
/// only present when the operator ticked them; nothing else about the station is added.
/// </summary>
/// <param name="Message">What the operator wrote.</param>
/// <param name="ReplyTo">An email address for a reply, or null to send without one.</param>
/// <param name="SystemInfo">App version and operating system, or null when the operator left it out.</param>
/// <param name="Log">The end of the app's log, or null when the operator left it out.</param>
public sealed record FeedbackReport(string Message, string? ReplyTo, string? SystemInfo, string? Log)
{
    /// <summary>The longest message accepted, in characters.</summary>
    public const int MaxMessageLength = 5000;

    /// <summary>The subject line: the start of the message, so the inbox shows what each report is about.</summary>
    public string Subject
    {
        get
        {
            var first = Message.Trim().Split('\n', 2)[0].Trim();
            if (first.Length > 60) first = first[..57].TrimEnd() + "…";
            return first.Length == 0 ? "Costas feedback" : $"Costas feedback: {first}";
        }
    }

    /// <summary>The full text: the message, then the parts the operator chose to include.</summary>
    public string Text(bool includeLog = true)
    {
        var sb = new StringBuilder(Message.Trim());
        if (!string.IsNullOrWhiteSpace(ReplyTo)) sb.Append("\n\nReply to: ").Append(ReplyTo.Trim());
        if (!string.IsNullOrWhiteSpace(SystemInfo)) sb.Append("\n\n").Append(SystemInfo.Trim());
        if (includeLog && !string.IsNullOrWhiteSpace(Log)) sb.Append("\n\n--- Recent log ---\n").Append(Log.TrimEnd());
        return sb.ToString();
    }

    /// <summary>True when an optional reply address looks like an email address (or is empty).</summary>
    public static bool IsReplyAddressValid(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return true;
        var a = address.Trim();
        var at = a.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at == a.LastIndexOf('@') && a.IndexOf('.', at) > at + 1 && !a.EndsWith('.') && !a.Any(char.IsWhiteSpace);
    }
}
