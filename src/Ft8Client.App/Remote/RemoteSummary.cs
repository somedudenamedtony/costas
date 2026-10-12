// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>The band summary on the phone, the same figures as the top of the Operate view.</summary>
/// <param name="Title">"20 m right now".</param>
/// <param name="StationsHeard">Stations heard.</param>
/// <param name="Countries">Countries.</param>
/// <param name="CallingCq">Calling CQ.</param>
/// <param name="YouNeed">You need.</param>
/// <param name="Caption">The line under the figures.</param>
public sealed record RemoteSummary(string Title, string StationsHeard, string Countries, string CallingCq, string YouNeed, string Caption);
