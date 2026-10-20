// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>A callbook that looks up one call at a time: QRZ XML, or HamQTH for operators without a QRZ subscription.</summary>
public interface ICallbookClient
{
    /// <summary>Looks up a call. Returns null when the callbook does not know it; throws when it cannot be asked.</summary>
    Task<QrzLookupResult?> LookupAsync(string call, CancellationToken ct);
}
