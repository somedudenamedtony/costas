// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>An 8-bit RGB colour from a <c>QColor</c> in a datagram.</summary>
public sealed record WsjtxColor(byte A, byte R, byte G, byte B);
