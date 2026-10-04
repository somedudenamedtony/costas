// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Messages;

/// <summary>The kinds of FT8/FT4 message the app understands.</summary>
public enum MessageKind
{
    /// <summary><c>CQ [modifier] CALL [GRID4]</c>.</summary>
    Cq,

    /// <summary><c>TO FROM GRID4</c>.</summary>
    GridReply,

    /// <summary><c>TO FROM ±NN</c>.</summary>
    Report,

    /// <summary><c>TO FROM R±NN</c>.</summary>
    RogerReport,

    /// <summary><c>TO FROM RRR</c>.</summary>
    Roger,

    /// <summary><c>TO FROM RR73</c>.</summary>
    RogerBye,

    /// <summary><c>TO FROM 73</c>.</summary>
    Bye,

    /// <summary><c>TO FROM</c> with no third field.</summary>
    Bare,

    /// <summary><c>CALL1 RR73; CALL2 &lt;FOX&gt; ±NN</c>.</summary>
    DxpeditionMulti,

    /// <summary>Two calls followed by a contest or other exchange the app does not interpret.</summary>
    Exchange,

    /// <summary>Anything else, up to 13 characters.</summary>
    FreeText,
}
