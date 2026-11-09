// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>A receive or tuner setting on the radio that Costas can read and change over CAT. None of them keys the radio.</summary>
public enum RigControl
{
    /// <summary>Audio (speaker) gain, 0 to 100 percent (Hamlib <c>AF</c>).</summary>
    AfGain,

    /// <summary>RF gain, 0 to 100 percent (Hamlib <c>RF</c>).</summary>
    RfGain,

    /// <summary>Preamp in dB as Hamlib lists it, 0 for off (Hamlib <c>PREAMP</c>).</summary>
    Preamp,

    /// <summary>Attenuator in dB, 0 for off (Hamlib <c>ATT</c>).</summary>
    Attenuator,

    /// <summary>AGC speed as Hamlib's number for it (Hamlib <c>AGC</c>).</summary>
    Agc,

    /// <summary>Noise blanker on (1) or off (0) (Hamlib function <c>NB</c>).</summary>
    NoiseBlanker,

    /// <summary>Noise reduction on (1) or off (0) (Hamlib function <c>NR</c>).</summary>
    NoiseReduction,

    /// <summary>Antenna tuner in line (1) or bypassed (0) (Hamlib function <c>TUNER</c>). Switching it never starts a tune.</summary>
    Tuner,
}
