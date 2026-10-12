// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A station calling me, as the phone shows it.</summary>
/// <param name="Call">Its call.</param>
/// <param name="Place">Where it is.</param>
/// <param name="Need">Need tag, or blank.</param>
/// <param name="Line">"Called you at 20:29:15 · -12".</param>
/// <param name="Button">"Answer", "Answer after this contact", "Resume", "Will answer after this contact".</param>
/// <param name="Command">"answer" or "answerAfter".</param>
/// <param name="Queued">Already set to be answered after this contact.</param>
/// <param name="Note">"Replied after you stopped: …", or blank.</param>
public sealed record RemoteCaller(string Call, string Place, string Need, string Line, string Button, string Command, bool Queued, string Note);
