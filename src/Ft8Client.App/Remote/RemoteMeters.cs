// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>The radio's transmit meters, as the desk's meter strip shows them.</summary>
/// <param name="Live">True while on the air; false for the last transmission.</param>
/// <param name="Caption">"TX" or "Last TX".</param>
/// <param name="Watts">"46 W", or null when the radio does not report power output.</param>
/// <param name="WattsBar">Power output as a fraction of the radio's most.</param>
/// <param name="Alc">"OK" or "High", or null when not reported.</param>
/// <param name="AlcBar">ALC, 0 to 1.</param>
/// <param name="AlcHigh">True when ALC is high enough to warn about.</param>
/// <param name="Swr">"1.3", or null when not reported.</param>
/// <param name="SwrBar">SWR from 1 (empty) to the critical level (full).</param>
/// <param name="SwrLevel">"", "caution" or "critical".</param>
/// <param name="Tip">The advice for a warning, or what the meters are.</param>
public sealed record RemoteMeters(bool Live, string Caption, string? Watts, double WattsBar, string? Alc, double AlcBar, bool AlcHigh,
                                  string? Swr, double SwrBar, string SwrLevel, string Tip);
