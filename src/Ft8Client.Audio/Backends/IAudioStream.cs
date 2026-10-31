// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Audio.Backends;

/// <summary>
/// Audio produced while it plays (keyboard-mode transmit). The output's audio thread reads it; reading never blocks,
/// allocates or locks.
/// </summary>
public interface IAudioStream
{
    /// <summary>Fills the start of <paramref name="destination"/> with mono samples; returns how many. Fewer than asked means none were ready yet (the rest is played as silence) or the stream has finished.</summary>
    int Read(Span<float> destination);

    /// <summary>True once every sample has been read and no more will come.</summary>
    bool Finished { get; }
}
