// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>One country still needed, with when the operator has heard it.</summary>
/// <param name="Name">"Mongolia".</param>
/// <param name="Detail">"Asia · not worked" or "Asia · worked, not confirmed".</param>
/// <param name="When">"Heard 6 times, always on 20 m between 13:00 and 15:00 UTC, last heard 3 Oct." or "Not heard in your decodes yet."</param>
/// <param name="Heard">True when there is receive history.</param>
public sealed record NeededCountryViewModel(string Name, string Detail, string When, bool Heard);
