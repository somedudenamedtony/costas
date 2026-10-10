// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>
/// One reading of the radio's transmit meters as Hamlib reports them: ALC from 0 to 1 of the radio's ALC scale, SWR
/// as a ratio. A value is null when the radio does not report that meter.
/// </summary>
public sealed record TxMeterReading(double? Alc, double? Swr)
{
    /// <summary>A reading with no meters.</summary>
    public static TxMeterReading None { get; } = new(null, null);
}
