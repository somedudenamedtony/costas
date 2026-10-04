// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>How a decode run ended.</summary>
public enum DecodeStatus
{
    /// <summary>The decoder finished and its output was read.</summary>
    Ok,

    /// <summary>The decoder exceeded its time limit and was killed.</summary>
    TimedOut,

    /// <summary>The decoder could not be started or exited with an error.</summary>
    Failed,

    /// <summary>The caller cancelled the run.</summary>
    Cancelled,
}
