// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Decoding;

namespace Ft8Client.App.Engine;

/// <summary>
/// Simulation: replays the WAV files of a folder slot by slot, aligned to the real slot clock, looping. Files of the
/// other mode are skipped. A mixer can add synthesised signals (the simulated partner).
/// </summary>
public sealed class SimulatedSlotSource : ISlotSource
{
    private readonly List<string> _files;
    private int _next;
    private bool _active;

    /// <summary>Creates a source over a folder (searched recursively) or a single file.</summary>
    public SimulatedSlotSource(string folder)
    {
        _files = File.Exists(folder)
            ? [folder]
            : Directory.EnumerateFiles(folder, "*.wav", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        if (_files.Count == 0) throw new FileNotFoundException("No WAV files to simulate with.", folder);
    }

    /// <summary>Extra signals to mix into a slot (12 kHz samples), e.g. the simulated partner.</summary>
    public Func<DateTime, Mode, float[]?>? Mixer { get; set; }

    /// <summary>File names in play order.</summary>
    public IReadOnlyList<string> Files => _files;

    /// <inheritdoc />
    public void BeginSlot(DateTime slotStartUtc, Mode mode) => _active = true;

    /// <inheritdoc />
    public void CancelSlot() => _active = false;

    /// <inheritdoc />
    public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode)
    {
        if (!_active) return null;
        _active = false;
        short[]? samples = null;
        for (var tries = 0; tries < _files.Count && samples is null; tries++)
        {
            var f = SampleFile.Load(_files[_next]);
            _next = (_next + 1) % _files.Count;
            if (f.Audio.Mode == mode) samples = f.Audio.Samples12k;
        }
        samples ??= new short[ModeInfo.DecoderSamples(mode)];
        var mix = Mixer?.Invoke(slotStartUtc, mode);
        if (mix is not null)
        {
            samples = (short[])samples.Clone();
            for (var i = 0; i < Math.Min(mix.Length, samples.Length); i++)
                samples[i] = (short)Math.Clamp(samples[i] + mix[i] * 32767f, short.MinValue, short.MaxValue);
        }
        return new SlotAudio(slotStartUtc, mode, samples);
    }
}
