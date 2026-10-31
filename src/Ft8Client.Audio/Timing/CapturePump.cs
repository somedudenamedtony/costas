// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Dsp;
using Ft8Client.Core;

namespace Ft8Client.Audio.Timing;

/// <summary>
/// Moves samples from the capture ring buffer through the resampler into the slot recorder, every 10 ms on its own
/// thread. Tracks the input level and dropouts.
/// </summary>
public sealed class CapturePump : IDisposable
{
    private readonly IAudioInput _input;
    private readonly SlotRecorder _recorder;
    private readonly Resampler _resampler;
    private readonly float[] _in = new float[8192];
    private readonly float[] _out;
    private Thread? _thread;
    private volatile bool _run;
    private long _lastOverruns;
    private long _samples;
    private int _peakBits;

    /// <summary>Creates a pump for an open input.</summary>
    public CapturePump(IAudioInput input, SlotRecorder recorder)
    {
        _input = input;
        _recorder = recorder;
        _resampler = new Resampler(input.SampleRate, ModeInfo.DecoderSampleRate);
        _out = new float[_resampler.MaxOutput(_in.Length)];
    }

    /// <summary>Live receive spectrum, fed outside the audio callback.</summary>
    public ReceiveSpectrum Spectrum { get; } = new();

    /// <summary>Recent RMS input level in dBFS.</summary>
    public double LevelDbfs { get; private set; } = -120;

    /// <summary>Input samples received since start (at the device rate).</summary>
    public long SamplesReceived => Interlocked.Read(ref _samples);

    /// <summary>
    /// The largest sample magnitude since the last call, in dBFS (-120 for silence or nothing received), and starts a new
    /// window. For the receive-level watch, read about once a second from another thread.
    /// </summary>
    public double TakePeakDbfs()
    {
        var peak = BitConverter.Int32BitsToSingle(Interlocked.Exchange(ref _peakBits, 0));
        return peak <= 1e-6f ? -120 : 20 * Math.Log10(peak);
    }

    /// <summary>Dropout events (ring buffer overruns) since start.</summary>
    public int Dropouts { get; private set; }

    /// <summary>Starts the pump thread.</summary>
    public void Start()
    {
        _run = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "CapturePump", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    /// <summary>Moves what is waiting now. Returns samples delivered at 12 kHz.</summary>
    public int PumpOnce()
    {
        var total = 0;
        int n;
        while ((n = _input.Buffer.Read(_in)) > 0)
        {
            LevelDbfs = AudioLevel.RmsDbfs(_in.AsSpan(0, n));
            Interlocked.Add(ref _samples, n);
            KeepPeak(_in.AsSpan(0, n));
            var m = _resampler.Process(_in.AsSpan(0, n), _out);
            _recorder.Append(_out.AsSpan(0, m));
            Spectrum.Append(_out.AsSpan(0, m));
            total += m;
        }
        var over = _input.Buffer.Overruns;
        if (over != _lastOverruns)
        {
            Dropouts++;
            _lastOverruns = over;
        }
        return total;
    }

    private void KeepPeak(ReadOnlySpan<float> block)
    {
        float peak = 0;
        foreach (var v in block) peak = Math.Max(peak, Math.Abs(v));
        int seen, bits = BitConverter.SingleToInt32Bits(peak);
        while (peak > BitConverter.Int32BitsToSingle(seen = Volatile.Read(ref _peakBits)) && Interlocked.CompareExchange(ref _peakBits, bits, seen) != seen)
        {
        }
    }

    private void Loop()
    {
        while (_run)
        {
            PumpOnce();
            Thread.Sleep(10);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
    }
}
