// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Engine;

/// <summary>An RTTY power cap on the radio: the operator's setting to put back and the capped setting read back.</summary>
/// <param name="Restore">The operator's power setting, as a fraction of the radio's maximum.</param>
/// <param name="Capped">The setting the radio reported after capping (a radio with coarse steps rounds it).</param>
public sealed record RttyPowerCap(double Restore, double Capped);
