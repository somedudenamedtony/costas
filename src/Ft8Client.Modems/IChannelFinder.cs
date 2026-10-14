// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems;

/// <summary>Finds keyboard-mode signals in the passband for a <see cref="ChannelBank"/>.</summary>
public interface IChannelFinder
{
    /// <summary>Feeds audio.</summary>
    void Process(ReadOnlySpan<float> samples);

    /// <summary>The signals found, as the frequencies a receiver tunes to, lowest first.</summary>
    IReadOnlyList<double> Signals();

    /// <summary>The SNR of the signal tuned at <paramref name="hz"/> in dB in 2,500 Hz, or null when unknown.</summary>
    int? SnrDb(double hz);
}
