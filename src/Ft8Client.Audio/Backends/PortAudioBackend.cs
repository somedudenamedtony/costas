// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Ft8Client.Audio.Dsp;
using static Ft8Client.Audio.Backends.PortAudioNative;

namespace Ft8Client.Audio.Backends;

/// <summary>
/// Linux audio through PortAudio (ALSA, which also reaches PulseAudio and PipeWire through their "pulse" and
/// "pipewire" devices). Devices are opened by id ("host API:device name"); the default device is never used
/// implicitly. Each open device has its own thread doing blocking reads or writes: capture mixes to mono and writes a
/// lock-free ring buffer, playback keeps the stream open and plays silence between transmissions. Those threads do
/// not allocate, lock or log.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class PortAudioBackend : IAudioBackend
{
    private const int InputFrames = 1024;
    private const int OutputFrames = 512;
    private static readonly object Gate = new();
    private static bool _initialized;
    private static int _openStreams;

    private PortAudioBackend()
    {
    }

    /// <summary>
    /// Starts PortAudio. Returns null and why when the library is missing (the <c>libportaudio2</c> package) or does
    /// not start.
    /// </summary>
    public static PortAudioBackend? TryCreate(out string? why)
    {
        why = null;
        try
        {
            lock (Gate) EnsureInitialized();
            return new PortAudioBackend();
        }
        catch (DllNotFoundException)
        {
            why = "PortAudio is not installed (install the libportaudio2 package).";
        }
        catch (InvalidOperationException ex)
        {
            why = ex.Message;
        }
        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListInputs() => List(input: true);

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListOutputs() => List(input: false);

    /// <inheritdoc />
    public IAudioInput OpenInput(string deviceId, int sampleRate)
    {
        lock (Gate)
        {
            EnsureInitialized();
            var stream = Open(deviceId, sampleRate, input: true, out var rate, out var channels);
            return new Input(stream, rate, channels);
        }
    }

    /// <inheritdoc />
    public IAudioOutput OpenOutput(string deviceId, int sampleRate)
    {
        lock (Gate)
        {
            EnsureInitialized();
            var stream = Open(deviceId, sampleRate, input: false, out var rate, out var channels);
            return new Output(stream, rate, channels);
        }
    }

    /// <inheritdoc />
    public unsafe string? DefaultOutputId()
    {
        lock (Gate)
        {
            EnsureInitialized();
            var i = GetDefaultOutputDevice();
            return i < 0 ? null : Describe(i).Id;
        }
    }

    private static unsafe List<AudioDevice> List(bool input)
    {
        lock (Gate)
        {
            // PortAudio lists devices as they were at start; start it again to see devices plugged in since, but only
            // when nothing is open, as stopping it closes every stream.
            if (_initialized && _openStreams == 0)
            {
                _ = Terminate();
                _initialized = false;
            }
            EnsureInitialized();
            var list = new List<AudioDevice>();
            var n = GetDeviceCount();
            for (var i = 0; i < n; i++)
            {
                var info = GetDeviceInfo(i);
                if (info is null || (input ? info->MaxInputChannels : info->MaxOutputChannels) <= 0) continue;
                list.Add(Describe(i));
            }
            return list;
        }
    }

    private static unsafe AudioDevice Describe(int index)
    {
        var info = GetDeviceInfo(index);
        var name = Marshal.PtrToStringUTF8(info->Name) ?? $"Device {index}";
        var host = GetHostApiInfo(info->HostApi);
        var hostName = host is null ? "?" : Marshal.PtrToStringUTF8(host->Name) ?? "?";
        return new AudioDevice($"{hostName}:{name}", hostName == "ALSA" ? name : $"{name} ({hostName})");
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        Check(Initialize(), "PortAudio did not start");
        _initialized = true;
    }

    private static unsafe IntPtr Open(string deviceId, int sampleRate, bool input, out int rate, out int channels)
    {
        var n = GetDeviceCount();
        var ids = new List<string>(n);
        for (var i = 0; i < n; i++) ids.Add(Describe(i).Id);
        var index = AlsaDeviceIds.Find(ids, deviceId);
        if (index < 0) throw new InvalidOperationException($"The configured {(input ? "input" : "output")} device is missing.");
        var info = GetDeviceInfo(index);
        var max = input ? info->MaxInputChannels : info->MaxOutputChannels;
        if (max <= 0) throw new InvalidOperationException($"\"{ids[index]}\" has no {(input ? "input" : "output")} channels.");

        // Stereo when the device has it (radios' USB codecs usually do; capture mixes it to mono), at the asked
        // rate, else the device's own.
        var p = new StreamParameters
        {
            Device = index,
            ChannelCount = Math.Min(2, max),
            SampleFormat = new CULong((nuint)Float32),
            // The high latency avoids underruns, which would put clicks into transmit audio; it is constant, so it
            // shows only as a fixed DT of a few tens of milliseconds.
            SuggestedLatency = input ? info->DefaultHighInputLatency : info->DefaultHighOutputLatency,
        };
        rate = sampleRate;
        if ((input ? IsFormatSupported(&p, null, rate) : IsFormatSupported(null, &p, rate)) != FormatIsSupported)
            rate = (int)info->DefaultSampleRate;
        var frames = new CULong((nuint)(input ? InputFrames : OutputFrames));
        var err = input
            ? OpenStream(out var stream, &p, null, rate, frames, new CULong((nuint)ClipOff), IntPtr.Zero, IntPtr.Zero)
            : OpenStream(out stream, null, &p, rate, frames, new CULong((nuint)ClipOff), IntPtr.Zero, IntPtr.Zero);
        Check(err, $"\"{ids[index]}\" could not be opened");
        channels = p.ChannelCount;
        _openStreams++;
        return stream;
    }

    private static void Close(IntPtr stream)
    {
        lock (Gate)
        {
            _ = StopStream(stream);
            _ = CloseStream(stream);
            _openStreams--;
        }
    }

    private sealed class Input : IAudioInput
    {
        private readonly IntPtr _stream;
        private readonly int _channels;
        private readonly float[] _raw;
        private readonly float[] _mono = new float[InputFrames];
        private readonly Thread _thread;
        private volatile bool _running;
        private bool _disposed;

        public Input(IntPtr stream, int rate, int channels)
        {
            _stream = stream;
            _channels = channels;
            _raw = new float[InputFrames * channels];
            SampleRate = rate;
            Buffer = new SpscRingBuffer(rate * 8);
            _thread = new Thread(Run) { IsBackground = true, Name = "Audio input", Priority = ThreadPriority.AboveNormal };
        }

        public int SampleRate { get; }

        public SpscRingBuffer Buffer { get; }

        public event EventHandler<string>? Stopped;

        public void Start()
        {
            Check(StartStream(_stream), "Audio input did not start");
            _running = true;
            _thread.Start();
        }

        // Audio thread: no allocation, locks, logging or exceptions.
        private unsafe void Run()
        {
            while (_running)
            {
                int err;
                fixed (float* p = _raw) err = ReadStream(_stream, p, new CULong(InputFrames));
                if (err != NoError && err != InputOverflowed)
                {
                    if (_running) ThreadPool.QueueUserWorkItem(_ => Stopped?.Invoke(this, $"Audio input stopped: {ErrorText(err)}"));
                    return;
                }
                for (var f = 0; f < InputFrames; f++)
                {
                    float sum = 0;
                    for (var c = 0; c < _channels; c++) sum += _raw[f * _channels + c];
                    _mono[f] = sum / _channels;
                }
                Buffer.Write(_mono);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _running = false;
            if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(1));
            Close(_stream);
        }
    }

    private sealed class Output : IAudioOutput
    {
        private readonly IntPtr _stream;
        private readonly int _channels;
        private readonly float[] _buffer;
        private readonly Thread _thread;
        private float[]? _current;
        private IAudioStream? _source;
        private readonly float[] _mono = new float[OutputFrames];
        private int _pos;
        private volatile bool _running = true;
        private bool _disposed;

        public Output(IntPtr stream, int rate, int channels)
        {
            _stream = stream;
            _channels = channels;
            _buffer = new float[OutputFrames * channels];
            SampleRate = rate;
            try
            {
                Check(StartStream(stream), "Audio output did not start");
            }
            catch
            {
                Close(stream);
                throw;
            }
            _thread = new Thread(Run) { IsBackground = true, Name = "Audio output", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        public int SampleRate { get; }

        public bool IsPlaying => Volatile.Read(ref _current) is not null || Volatile.Read(ref _source) is not null;

        public event EventHandler<string>? Faulted;

        public void Play(float[] samples)
        {
            Volatile.Write(ref _source, null);
            _pos = 0;
            Volatile.Write(ref _current, samples);
        }

        public void PlayStream(IAudioStream stream)
        {
            Volatile.Write(ref _current, null);
            Volatile.Write(ref _source, stream);
        }

        public void Stop()
        {
            Volatile.Write(ref _current, null);
            Volatile.Write(ref _source, null);
        }

        // Audio thread: plays the current buffer, silence otherwise. Keeps the stream open for low start latency.
        private unsafe void Run()
        {
            while (_running)
            {
                Array.Clear(_buffer);
                var cur = Volatile.Read(ref _current);
                if (Volatile.Read(ref _source) is { } stream)
                {
                    var got = stream.Read(_mono);
                    for (var i = 0; i < got; i++)
                    {
                        for (var c = 0; c < _channels; c++) _buffer[i * _channels + c] = _mono[i];
                    }
                    if (stream.Finished) Interlocked.CompareExchange(ref _source, null, stream);
                }
                else if (cur is not null)
                {
                    var n = Math.Min(OutputFrames, cur.Length - _pos);
                    for (var i = 0; i < n; i++)
                    {
                        var v = cur[_pos + i];
                        for (var c = 0; c < _channels; c++) _buffer[i * _channels + c] = v;
                    }
                    _pos += n;
                    if (_pos >= cur.Length) Interlocked.CompareExchange(ref _current, null, cur);
                }
                int err;
                fixed (float* p = _buffer) err = WriteStream(_stream, p, new CULong(OutputFrames));
                if (err != NoError && err != OutputUnderflowed)
                {
                    Volatile.Write(ref _current, null);
                    Volatile.Write(ref _source, null);
                    if (_running) ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(this, $"Audio output stopped: {ErrorText(err)}"));
                    return;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _running = false;
            Stop();
            if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(1));
            Close(_stream);
        }
    }
}
