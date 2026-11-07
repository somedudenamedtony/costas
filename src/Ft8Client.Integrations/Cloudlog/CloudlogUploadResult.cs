// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Cloudlog;

/// <summary>What a Wavelog or Cloudlog upload came to.</summary>
/// <param name="Accepted">The contact is in the log now (added, or already there).</param>
/// <param name="Error">Why not, in words for the status line; never contains the API key.</param>
/// <param name="Permanent">True when retrying soon cannot help (wrong key or station id): retry slowly.</param>
public sealed record CloudlogUploadResult(bool Accepted, string? Error = null, bool Permanent = false);
