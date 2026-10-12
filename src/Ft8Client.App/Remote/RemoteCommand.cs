// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A message from the remote page.</summary>
/// <param name="Type">
/// "ping", "call", "answer", "answerAfter", "cq", "resend", "logNow", "jump", "abandon", "halt", "band", "offset",
/// "offsetAuto", or "show" (open a station's details; blank call closes them).
/// </param>
/// <param name="Call">The station, for call, answer and show.</param>
/// <param name="Step">The step index, for jump.</param>
/// <param name="Band">The band, "20m", for band.</param>
/// <param name="Mode">The mode, "FT8" or "FT4", for band.</param>
/// <param name="Hz">The Tx offset in hertz, for offset.</param>
public sealed record RemoteCommand(string Type, string? Call = null, int? Step = null, string? Band = null, string? Mode = null, int? Hz = null);
