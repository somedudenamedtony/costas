// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>A QRZ request failed. <see cref="IsAuth"/> is true when the key lacks privileges or is wrong.</summary>
public sealed class QrzException(string message, bool isAuth = false, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>True for <c>RESULT=AUTH</c>.</summary>
    public bool IsAuth { get; } = isAuth;
}
