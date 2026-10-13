// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>Which confirmations count when the operator asks to count only confirmed contacts.</summary>
public enum ConfirmationSource
{
    /// <summary>Any confirmation: QRZ, LoTW or a QSL card.</summary>
    Any,

    /// <summary>LoTW only: what DXCC and WAS credit.</summary>
    Lotw,
}
