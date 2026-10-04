// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Contacts;

namespace Ft8Client.App.Engine;

/// <summary>A transmission rendered and checked, waiting for its slot.</summary>
/// <param name="Plan">What and when.</param>
/// <param name="Mode">Mode.</param>
/// <param name="OffsetHz">Transmit offset.</param>
/// <param name="Samples">Audio at the output rate.</param>
/// <param name="SampleRate">Output rate.</param>
public sealed record PreparedTx(TxPlan Plan, Mode Mode, int OffsetHz, float[] Samples, int SampleRate)
{
    /// <summary>Audio duration.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
