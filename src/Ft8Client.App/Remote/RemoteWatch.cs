// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A row of Watching on the phone: needed, but not callable now.</summary>
/// <param name="Call">Its call.</param>
/// <param name="Where">Place and distance.</param>
/// <param name="Need">Need tag.</param>
/// <param name="Reason">Why it cannot be called now.</param>
public sealed record RemoteWatch(string Call, string Where, string Need, string Reason);
