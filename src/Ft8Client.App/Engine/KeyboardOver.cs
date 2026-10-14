// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Engine;

/// <summary>How a keyboard-mode over ended.</summary>
/// <param name="Sent">The text that went out.</param>
/// <param name="Macros">The keys of the macros sent whole, in order.</param>
/// <param name="Completed">True when it ended normally (everything sent, then back to receive).</param>
/// <param name="Watchdog">True when the keyboard watchdog ended it.</param>
public sealed record KeyboardOver(string Sent, IReadOnlyList<int> Macros, bool Completed, bool Watchdog);
