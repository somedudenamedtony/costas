// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Keyboard;

/// <summary>A piece of a macro as the type-ahead line shows it: plain text, a filled field, or a field with no value.</summary>
/// <param name="Text">The text, or the field as written (such as <c>{NAME}</c>) when it has no value.</param>
/// <param name="Missing">True for a field with no value, shown in red; the macro is not sent until it is filled.</param>
public sealed record MacroSegment(string Text, bool Missing);
