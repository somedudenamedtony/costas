// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>A station calling the one in the details flyout or the contact.</summary>
/// <param name="Call">Caller.</param>
/// <param name="Snr">Its SNR at my station, "-06".</param>
/// <param name="Offset">Its audio offset, "1330 Hz".</param>
public sealed record PileupCallerViewModel(string Call, string Snr, string Offset);
