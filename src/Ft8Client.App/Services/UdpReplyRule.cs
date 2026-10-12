// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.App.Services;

/// <summary>
/// Which station a Reply from another program (GridTracker, JTAlert) calls. Like WSJT-X, it acts only when the Reply
/// describes one of our own recent decodes exactly, and only on a CQ; this app also answers a station calling me, as a
/// double-click in Raw decodes would. Never on my own transmissions.
/// </summary>
public static class UdpReplyRule
{
    /// <summary>The call to call or answer, or null when the Reply is to be ignored.</summary>
    public static string? Sender(WsjtxReply reply, IReadOnlyList<RawDecodeLine> recent)
    {
        var text = reply.Message.Trim();
        var line = recent.LastOrDefault(l => !l.Transmitted
            && string.Equals(l.Decode.Text.Trim(), text, StringComparison.Ordinal)
            && Math.Abs((l.Decode.SlotStartUtc.TimeOfDay - reply.TimeOfDay).TotalSeconds) < 1);
        if (line is null || !(line.IsCq || line.ToMe)) return null;
        return line.Message.SenderCall;
    }
}
