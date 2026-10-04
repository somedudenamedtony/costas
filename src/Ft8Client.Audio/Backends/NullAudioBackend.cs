// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;

namespace Ft8Client.Audio.Backends;

/// <summary>No audio hardware: one silent input and one recording null output. Used in simulation and tests.</summary>
public sealed class NullAudioBackend : IAudioBackend
{
    /// <summary>The single device id.</summary>
    public const string DeviceId = "null";

    /// <summary>The last output opened.</summary>
    public NullAudioOutput? LastOutput { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListInputs() => [new(DeviceId, "No audio (simulation)")];

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListOutputs() => [new(DeviceId, "No audio (simulation)")];

    /// <inheritdoc />
    public IAudioInput OpenInput(string deviceId, int sampleRate) => new SilentInput(sampleRate);

    /// <inheritdoc />
    public IAudioOutput OpenOutput(string deviceId, int sampleRate) => LastOutput = new NullAudioOutput(sampleRate);

    private sealed class SilentInput(int rate) : IAudioInput
    {
        public int SampleRate { get; } = rate;

        public SpscRingBuffer Buffer { get; } = new(rate * 4);

        public event EventHandler<string>? Stopped
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public void Dispose()
        {
        }
    }
}
