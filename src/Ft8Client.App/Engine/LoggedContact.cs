// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Logbook;

namespace Ft8Client.App.Engine;

/// <summary>The contact logged most recently, for the "Logged" notice on the desk and the phone.</summary>
/// <param name="Id">Counts up with each contact logged this session, so a notice shows once per contact.</param>
/// <param name="Call">The call logged.</param>
/// <param name="Need">What it was new for when logged.</param>
/// <param name="Place">"Indiana, USA", or empty when unknown.</param>
/// <param name="Band">"20m".</param>
/// <param name="Mode">The mode.</param>
/// <param name="ReportSent">Report sent in dB.</param>
/// <param name="ReportReceived">Report received in dB, null when none was exchanged.</param>
/// <param name="LoggedUtc">When it was logged.</param>
/// <param name="QsoId">Its row in the log, for editing, or null when it could not be stored.</param>
/// <param name="PowerWatts">Power logged ("25"), as read from the radio or set in Settings, or null.</param>
public sealed record LoggedContact(long Id, string Call, NeedTag Need, string Place, string Band, Mode Mode, int ReportSent, int? ReportReceived,
                                   DateTime LoggedUtc, long? QsoId = null, string? PowerWatts = null);
