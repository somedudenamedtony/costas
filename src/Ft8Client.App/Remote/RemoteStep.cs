// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>One box of the conversation strip on the phone.</summary>
/// <param name="Index">Step index, for jumping.</param>
/// <param name="Mine">My message or theirs.</param>
/// <param name="Who">"You" or the DX call.</param>
/// <param name="Time">UTC time, or blank.</param>
/// <param name="Message">Literal message.</param>
/// <param name="Status">"Sent", "Sending · 9 s left", "Waiting for reply", "Next".</param>
/// <param name="Look">"done", "sending", "waiting" or "upcoming".</param>
/// <param name="CanJump">True for my upcoming steps.</param>
public sealed record RemoteStep(int Index, bool Mine, string Who, string Time, string Message, string Status, string Look, bool CanJump);
