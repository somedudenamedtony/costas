// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Log;

/// <summary>Where a contact came from.</summary>
public static class QsoSource
{
    /// <summary>Made in this app.</summary>
    public const string Local = "local";

    /// <summary>Fetched from the QRZ logbook.</summary>
    public const string Qrz = "qrz";

    /// <summary>Imported from an ADIF file.</summary>
    public const string Adif = "adif";
}
