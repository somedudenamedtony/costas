// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>Who hears me: the "Your signal" panel of the Operate view.</summary>
/// <param name="Title">"Your signal on 20 m".</param>
/// <param name="Headline">"12 stations in 5 countries heard you in the last 15 minutes."</param>
/// <param name="Source">"PSK Reporter · live".</param>
/// <param name="Farthest">Farthest station and distance.</param>
/// <param name="Best">Best report.</param>
/// <param name="Neighbours">How nearby stations are doing, or blank.</param>
/// <param name="Reports">Latest reports, newest first.</param>
public sealed record RemoteSignal(string Title, string Headline, string Source, string Farthest, string Best, string Neighbours,
                                  IReadOnlyList<RemoteReport> Reports);
