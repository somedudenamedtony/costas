// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A message from the remote page.</summary>
/// <param name="Type">"ping", "call", "answer", "answerAfter", "cq", "resend", "logNow", "jump", "abandon", "halt".</param>
/// <param name="Call">The station, for call and answer.</param>
/// <param name="Step">The step index, for jump.</param>
public sealed record RemoteCommand(string Type, string? Call = null, int? Step = null);
