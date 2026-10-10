// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>How serious a transmit meter result is.</summary>
public enum TxMeterLevel
{
    /// <summary>Nothing to report.</summary>
    Ok,

    /// <summary>Worth fixing: ALC acting or SWR raised.</summary>
    Caution,

    /// <summary>Stop and check the antenna: SWR high.</summary>
    Critical,
}
