// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>One of the radio's settings, as the phone shows it.</summary>
/// <param name="Key">The setting, as the "radio" command names it ("preamp").</param>
/// <param name="Group">"receive" or "noise", for the tile it goes on.</param>
/// <param name="Label">"Preamp".</param>
/// <param name="Kind">"level" (0 to 100 percent), "choice" (one of <paramref name="Options"/>) or "toggle" (0 or 1).</param>
/// <param name="Value">The value now.</param>
/// <param name="Shown">The value as text: "60%", "Amp 1", "On".</param>
/// <param name="Options">The choices, for "choice".</param>
/// <param name="Hint">One line on what it does, or blank.</param>
public sealed record RemoteRadioControl(string Key, string Group, string Label, string Kind, int Value, string Shown,
                                        IReadOnlyList<RemoteRadioOption> Options, string Hint);
