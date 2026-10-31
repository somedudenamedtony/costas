// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Stations;

/// <summary>
/// Tells from one slot's decodes which stations run a Fox (a Fox/Hound DXpedition). A Fox gives itself away by a
/// multi-answer message (<c>CALL1 RR73; CALL2 &lt;FOX&gt; ±NN</c>), or by sending more than one message in the same slot
/// on different frequencies (its streams, up to five in WSJT-X), which an ordinary station never does.
/// </summary>
public static class FoxSigns
{
    /// <summary>Two messages from one sender at least this far apart are separate Fox streams, not one signal decoded twice.</summary>
    public const int StreamSpacingHz = 50;

    /// <summary>The senders that showed a Fox's signs in this slot's decodes. Low-confidence decodes are ignored.</summary>
    public static HashSet<string> Senders(IEnumerable<HeardMessage> heard)
    {
        var foxes = new HashSet<string>(StringComparer.Ordinal);
        var seen = new Dictionary<string, (string Text, int OffsetHz)>(StringComparer.Ordinal);
        foreach (var h in heard)
        {
            if (h.Decode.LowConfidence) continue;
            var sender = h.Message.SenderCall;
            if (sender is null || !Callsign.IsValid(sender)) continue;
            if (h.Message.Kind == MessageKind.DxpeditionMulti)
            {
                foxes.Add(sender);
                continue;
            }
            if (seen.TryGetValue(sender, out var first))
            {
                if (!string.Equals(first.Text, h.Decode.Text, StringComparison.Ordinal) && Math.Abs(first.OffsetHz - h.Decode.OffsetHz) >= StreamSpacingHz)
                    foxes.Add(sender);
            }
            else
            {
                seen[sender] = (h.Decode.Text, h.Decode.OffsetHz);
            }
        }
        return foxes;
    }
}
