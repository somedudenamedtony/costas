// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One band in the DXCC-by-band table.</summary>
/// <param name="Band">"20 m".</param>
/// <param name="Worked">"112".</param>
/// <param name="Confirmed">"87".</param>
/// <param name="Progress">Confirmed share of 100, 0 to 1.</param>
/// <param name="Note">"5-band" for the five 5-band DXCC bands, otherwise blank.</param>
public sealed record AwardBandRowViewModel(string Band, string Worked, string Confirmed, double Progress, string Note);
