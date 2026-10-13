// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Hunting;

namespace Ft8Client.App.ViewModels;

/// <summary>One Hunt list entry on the Settings page.</summary>
/// <param name="Target">The entry.</param>
/// <param name="Text">"Alaska (KL7) on 40 m".</param>
public sealed record HuntRowViewModel(HuntTarget Target, string Text);
