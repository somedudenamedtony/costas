// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Encoding;

namespace Ft8Client.Core.Contacts;

/// <summary>
/// The DX call and my call as a message addresses them. A standard call goes in full; a nonstandard (compound) call
/// can go in full only in a type 4 message, whose other call is hashed (<c>&lt;CALL&gt;</c>) and which carries no grid or
/// report. The forms follow WSJT-X <c>genStdMsgs</c> (widgets/mainwindow.cpp, git <c>b4f9a43</c>).
/// </summary>
/// <param name="Dx">The DX call.</param>
/// <param name="Me">My call.</param>
/// <param name="DxStandard">True when the DX call packs as a standard call.</param>
/// <param name="MeStandard">True when my call packs as a standard call.</param>
public readonly record struct CallPair(string Dx, string Me, bool DxStandard, bool MeStandard)
{
    /// <summary>The pair, each call checked against the encoder.</summary>
    public static CallPair Of(string dx, string me) => new(dx, me, MessagePacker.IsStandardCall(dx), MessagePacker.IsStandardCall(me));

    /// <summary>
    /// The calls for a message with a grid or report (my answer to a CQ, report, R-report): the nonstandard call is
    /// hashed, the standard one sent in full. With a nonstandard DX call the DX is hashed whatever my call is.
    /// </summary>
    public string WithPayload => !DxStandard ? $"<{Dx}> {Me}" : !MeStandard ? $"{Dx} <{Me}>" : $"{Dx} {Me}";

    /// <summary>
    /// The calls for a sign-off (<c>RRR</c>, <c>RR73</c>, <c>73</c>): the nonstandard call in full and the standard one
    /// hashed (a type 4 message), so the other station sees the full call it worked.
    /// </summary>
    public string ForSignoff => (DxStandard, MeStandard) switch
    {
        (false, true) => $"{Dx} <{Me}>",
        (true, false) => $"<{Dx}> {Me}",
        _ => $"{Dx} {Me}",
    };

    /// <summary>
    /// The calls for my answer to a CQ. A nonstandard call of mine goes in full with the DX call hashed and no grid (a
    /// type 4 message); otherwise as <see cref="WithPayload"/>.
    /// </summary>
    public string ForReply => MeStandard ? WithPayload : $"<{Dx}> {Me}";

    /// <summary>True when my answer to a CQ can carry my grid: a nonstandard call of mine leaves no room for it.</summary>
    public bool GridFits => MeStandard || !DxStandard;
}
