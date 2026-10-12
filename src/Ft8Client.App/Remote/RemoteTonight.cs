// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A contact made tonight.</summary>
/// <param name="Call">The call.</param>
/// <param name="Place">Its country.</param>
/// <param name="Band">"20 m".</param>
/// <param name="Need">What it was new for.</param>
/// <param name="Time">"20:31" UTC.</param>
public sealed record RemoteTonight(string Call, string Place, string Band, string Need, string Time);
