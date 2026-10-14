// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>
/// The RTTY power cap Costas has put on the radio, kept until the power is put back, so that after a crash the next
/// start restores the operator's own power instead of capping the cap again.
/// </summary>
public sealed class RttyPowerCapOptions
{
    /// <summary>The operator's power setting to put back, as a fraction of the radio's maximum.</summary>
    public double Restore { get; set; }

    /// <summary>The capped setting the radio reported back, as a fraction of its maximum.</summary>
    public double Capped { get; set; }

    /// <summary>Which radio settings it was capped on; a different radio is not given this power.</summary>
    public string RigKey { get; set; } = string.Empty;
}
