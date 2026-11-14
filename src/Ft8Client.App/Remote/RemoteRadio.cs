// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>The phone's Radio tab: Tune, the radio's meters and the settings it can change over CAT.</summary>
/// <param name="TuneText">"Tune", or "Stop tune" while a tune is going.</param>
/// <param name="CanTune">False while CQ, a contact or another transmission is under way, as on the desk.</param>
/// <param name="Tuning">True while a Tune tone or the radio's own tuner is going.</param>
/// <param name="CanChange">False while transmitting: settings only change between transmissions.</param>
/// <param name="Controls">The receive and tuner settings, in the order to show them; empty when the radio reports none.</param>
/// <param name="Meters">The power, ALC and SWR meters, live or from the last transmission; null before any reading.</param>
public sealed record RemoteRadio(string TuneText, bool CanTune, bool Tuning, bool CanChange, IReadOnlyList<RemoteRadioControl> Controls,
                                 RemoteMeters? Meters);
