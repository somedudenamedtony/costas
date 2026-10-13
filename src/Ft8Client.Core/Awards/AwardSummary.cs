// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Awards;

/// <summary>Progress towards every award the Awards view shows. Built by <see cref="AwardBuilder"/>.</summary>
/// <param name="Dxcc">DXCC on any band (mixed).</param>
/// <param name="DxccByBand">DXCC per band, in band-plan order: every band with contacts, and always the five 5-band DXCC bands.</param>
/// <param name="FiveBandConfirmed">How many of the five 5-band DXCC bands have 100 or more countries confirmed.</param>
/// <param name="FiveBandWorked">How many of the five bands have 100 or more countries worked.</param>
/// <param name="Was">Worked All States (the 50 US states).</param>
/// <param name="Waz">Worked All Zones (the 40 CQ zones, from the country file's prefix zones).</param>
/// <param name="Vucc">VUCC on 6 m: distinct 4-character grids; null when 6 m is not in the band plan.</param>
public sealed record AwardSummary(AwardProgress Dxcc, IReadOnlyList<AwardProgress> DxccByBand, int FiveBandConfirmed, int FiveBandWorked,
                                  AwardProgress Was, AwardProgress Waz, AwardProgress? Vucc);
