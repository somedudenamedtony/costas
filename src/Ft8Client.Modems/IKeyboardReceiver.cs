// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems;

/// <summary>A receiver for one keyboard-mode signal (PSK or RTTY): audio in, characters out.</summary>
public interface IKeyboardReceiver
{
    /// <summary>The frequency it started on.</summary>
    double StartFrequencyHz { get; }

    /// <summary>The frequency it is on now (moves with AFC): the carrier for PSK, midway between the tones for RTTY.</summary>
    double FrequencyHz { get; }

    /// <summary>Signal quality, 0 (noise) to 1 (clean).</summary>
    double Quality { get; }

    /// <summary>Feeds audio; returns the characters completed in it (empty when none).</summary>
    string Process(ReadOnlySpan<float> samples);
}
