// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;

namespace Ft8Client.App.Services;

/// <summary>One update from <see cref="InputLevelMonitor"/>.</summary>
/// <param name="RmsDbfs">Level, dBFS.</param>
/// <param name="State">How the level looks.</param>
/// <param name="Passed">The level has been good for long enough to pass the step.</param>
/// <param name="StoppedReason">Why the device stopped, if it did.</param>
public sealed record InputLevelReading(double RmsDbfs, InputLevelState State, bool Passed, string? StoppedReason);
