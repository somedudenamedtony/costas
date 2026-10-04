// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Ft8Client.Audio.Dsp;

// NAudio 3 marks WasapiCapture and WasapiOut obsolete in favour of WasapiRecorderBuilder and WasapiPlayerBuilder.
// The classic classes are the proven path; moving to the builders needs testing on Windows hardware (see
// docs/08-open-questions.md). The suppression is limited to this file.
#pragma warning disable CS0618

namespace Ft8Client.Audio.Backends;

/// <summary>
/// WASAPI shared mode through NAudio. Devices are opened by endpoint id; the default device is never used implicitly.
/// Capture runs at the device mix format; samples are mixed to mono and written to a lock-free ring buffer from the
/// callback without allocating, locking or logging.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiAudioBackend : IAudioBackend
{
    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListInputs() => List(DataFlow.Capture);

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListOutputs() => List(DataFlow.Render);

    /// <inheritdoc />
    public IAudioInput OpenInput(string deviceId, int sampleRate)
    {
        using var e = new MMDeviceEnumerator();
        var device = e.GetDevice(deviceId) ?? throw new InvalidOperationException("The configured input device is missing.");
        return new Input(device);
    }

    /// <inheritdoc />
    public IAudioOutput OpenOutput(string deviceId, int sampleRate)
    {
        using var e = new MMDeviceEnumerator();
        var device = e.GetDevice(deviceId) ?? throw new InvalidOperationException("The configured output device is missing.");
        return new Output(device);
    }

    private static List<AudioDevice> List(DataFlow flow)
    {
        using var e = new MMDeviceEnumerator();
        var list = new List<AudioDevice>();
        foreach (var d in e.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            list.Add(new AudioDevice(d.ID, d.FriendlyName));
            d.Dispose();
        }
        return list;
    }

    private sealed class Input : IAudioInput
    {
        private const int Chunk = 2048;
        private readonly MMDevice _device;
        private readonly WasapiCapture _capture;
        private readonly int _channels;
        private readonly bool _float;
        private readonly int _bytesPerSample;
        private readonly float[] _scratch = new float[Chunk];

        public Input(MMDevice device)
        {
            _device = device;
            _capture = new WasapiCapture(device, true, 20);
            var f = _capture.WaveFormat;
            _channels = f.Channels;
            _bytesPerSample = f.BitsPerSample / 8;
            _float = f.Encoding == WaveFormatEncoding.IeeeFloat || (f.Encoding == WaveFormatEncoding.Extensible && f.BitsPerSample == 32);
            SampleRate = f.SampleRate;
            Buffer = new SpscRingBuffer(SampleRate * 8);
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += (_, a) => ThreadPool.QueueUserWorkItem(_ => Stopped?.Invoke(this, a.Exception?.Message ?? "Audio input stopped."));
        }

        public int SampleRate { get; }

        public SpscRingBuffer Buffer { get; }

        public event EventHandler<string>? Stopped;

        public void Start() => _capture.StartRecording();

        // Audio thread: no allocation, locks, logging or exceptions.
        private void OnData(object? sender, WaveInEventArgs a)
        {
            var bytes = a.BufferSpan[..a.BytesRecorded];
            var frameBytes = _bytesPerSample * _channels;
            var frames = bytes.Length / frameBytes;
            var done = 0;
            while (done < frames)
            {
                var n = Math.Min(Chunk, frames - done);
                for (var f = 0; f < n; f++)
                {
                    float sum = 0;
                    var off = (done + f) * frameBytes;
                    for (var c = 0; c < _channels; c++)
                    {
                        var p = off + c * _bytesPerSample;
                        sum += _float
                            ? BitConverter.ToSingle(bytes.Slice(p, 4))
                            : _bytesPerSample == 2 ? BitConverter.ToInt16(bytes.Slice(p, 2)) / 32768f : BitConverter.ToInt32(bytes.Slice(p, 4)) / 2147483648f;
                    }
                    _scratch[f] = sum / _channels;
                }
                Buffer.Write(_scratch.AsSpan(0, n));
                done += n;
            }
        }

        public void Dispose()
        {
            try { _capture.StopRecording(); }
            catch (InvalidOperationException) { }
            _capture.Dispose();
            _device.Dispose();
        }
    }

    private sealed class Output : IAudioOutput, ISampleProvider
    {
        private readonly MMDevice _device;
        private readonly WasapiOut _out;
        private readonly int _channels;
        private float[]? _current;
        private int _pos;
        private volatile bool _stopping;

        public Output(MMDevice device)
        {
            _device = device;
            using var client = device.CreateAudioClient();
            var mix = client.MixFormat;
            SampleRate = mix.SampleRate;
            _channels = mix.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, _channels);
            _out = new WasapiOut(device, AudioClientShareMode.Shared, true, 30);
            _out.Init(new SampleToWaveProvider(this));
            _out.PlaybackStopped += (_, a) =>
            {
                if (!_stopping) Faulted?.Invoke(this, a.Exception?.Message ?? "Audio output stopped.");
            };
            _out.Play();
        }

        public int SampleRate { get; }

        public WaveFormat WaveFormat { get; }

        public bool IsPlaying => Volatile.Read(ref _current) is not null;

        public event EventHandler<string>? Faulted;

        public void Play(float[] samples)
        {
            _pos = 0;
            Volatile.Write(ref _current, samples);
        }

        public void Stop() => Volatile.Write(ref _current, null);

        // Audio thread: plays the current buffer, silence otherwise. Keeps the stream open for low start latency.
        public int Read(Span<float> buffer)
        {
            buffer.Clear();
            var cur = Volatile.Read(ref _current);
            if (cur is null) return buffer.Length;
            var frames = buffer.Length / _channels;
            var n = Math.Min(frames, cur.Length - _pos);
            for (var i = 0; i < n; i++)
            {
                var v = cur[_pos + i];
                for (var c = 0; c < _channels; c++) buffer[i * _channels + c] = v;
            }
            _pos += n;
            if (_pos >= cur.Length) Interlocked.CompareExchange(ref _current, null, cur);
            return buffer.Length;
        }

        public void Dispose()
        {
            _stopping = true;
            Stop();
            _out.Stop();
            _out.Dispose();
            _device.Dispose();
        }
    }
}
