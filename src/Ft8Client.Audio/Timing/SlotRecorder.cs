// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;

namespace Ft8Client.Audio.Timing;

/// <summary>
/// Collects 12 kHz samples for the current slot. At the capture cut-off the buffer is zero-padded to the decoder
/// length and returned. Called from the capture pump and the slot thread; not from the audio callback.
/// </summary>
public sealed class SlotRecorder
{
    private readonly object _gate = new();
    private readonly float[] _buffer = new float[ModeInfo.DecoderSamples(Mode.Ft8) + ModeInfo.DecoderSampleRate];
    private int _count;
    private DateTime _slot;
    private Mode _mode;
    private bool _active;

    /// <summary>Begins a new slot.</summary>
    public void BeginSlot(DateTime slotStartUtc, Mode mode)
    {
        lock (_gate)
        {
            _slot = slotStartUtc;
            _mode = mode;
            _count = 0;
            _active = true;
        }
    }

    /// <summary>Stops recording without producing a slot (we are transmitting).</summary>
    public void Cancel()
    {
        lock (_gate) _active = false;
    }

    /// <summary>Appends samples of the current slot.</summary>
    public void Append(ReadOnlySpan<float> samples12k)
    {
        lock (_gate)
        {
            if (!_active) return;
            var n = Math.Min(samples12k.Length, _buffer.Length - _count);
            samples12k[..n].CopyTo(_buffer.AsSpan(_count));
            _count += n;
        }
    }

    /// <summary>Ends the slot and returns its audio, or null if the slot was cancelled or never begun.</summary>
    public SlotAudio? Cutoff()
    {
        lock (_gate)
        {
            if (!_active) return null;
            _active = false;
            var samples = new short[ModeInfo.DecoderSamples(_mode)];
            var n = Math.Min(_count, samples.Length);
            AudioLevel.ToInt16(_buffer.AsSpan(0, n), samples);
            return new SlotAudio(_slot, _mode, samples);
        }
    }
}
