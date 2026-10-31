// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Integrations.Feedback;

/// <summary>
/// The email route: a <c>mailto:</c> link that opens the operator's own mail program with the report filled in, so
/// they see and send it themselves. Mail programs cut long links, so the log never goes in the link.
/// </summary>
public static class FeedbackMail
{
    /// <summary>The longest link made; Windows and several mail programs stop near 2,000 characters.</summary>
    public const int MaxLinkLength = 1900;

    /// <summary>True when <paramref name="address"/> can be put in a mailto link.</summary>
    public static bool IsAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address) && FeedbackReport.IsReplyAddressValid(address) && !address.Contains('?', StringComparison.Ordinal);

    /// <summary>The mailto link for a report, with the body shortened when it would make the link too long.</summary>
    public static string Link(string address, FeedbackReport report)
    {
        var head = $"mailto:{address.Trim()}?subject={Uri.EscapeDataString(report.Subject)}&body=";
        var body = report.Text(includeLog: false);
        var link = head + Uri.EscapeDataString(body);
        if (link.Length <= MaxLinkLength) return link;
        const string Cut = "\n\n[Shortened: use Copy in Costas to paste the whole report.]";
        var keep = body.Length;
        while (keep > 0 && (head + Uri.EscapeDataString(body[..keep] + Cut)).Length > MaxLinkLength)
        {
            keep -= Math.Max(1, keep / 10);
            if (keep > 0 && char.IsHighSurrogate(body[keep - 1])) keep--;
        }
        return head + Uri.EscapeDataString(body[..Math.Max(0, keep)] + Cut);
    }
}
