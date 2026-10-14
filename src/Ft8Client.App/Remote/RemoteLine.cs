// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Remote;

/// <summary>A line of the PSK or RTTY conversation on the phone.</summary>
/// <param name="Time">"20:29:31".</param>
/// <param name="Who">Who sent it.</param>
/// <param name="Text">What was sent.</param>
/// <param name="Mine">True for my own text, shown dimmed.</param>
public sealed record RemoteLine(string Time, string Who, string Text, bool Mine);
