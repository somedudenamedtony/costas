// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core;
using Ft8Client.Core.Time;
using Ft8Client.Decoding;

namespace Ft8Client.App.Engine;

/// <summary>
/// Simulation only (--simulate): plays the keyboard-mode recordings in <c>samples/psk31</c> or <c>samples/psk63</c>,
/// in a loop and in real time, into <see cref="KeyboardSession.Input"/> while the session is in that mode, as the
/// capture pump would from a radio.
/// </summary>
public sealed class SimulatedKeyboardAudio : IDisposable
{
    private const int StepMs = 100;
    private readonly string _samples;
    private readonly KeyboardSession _keyboard;
    private readonly Func<Mode> _mode;
    private readonly IClock _clock;
    private readonly string? _replay;
    private readonly Dictionary<Mode, float[]> _audio = [];
    private Mode _playing;
    private int _position;
    private long _lastTicks;
    private Thread? _thread;
    private volatile bool _run;

    /// <summary>Creates a player over a samples folder.</summary>
    public SimulatedKeyboardAudio(string samples, KeyboardSession keyboard, Func<Mode> mode, IClock clock, string? replay = null)
    {
        _samples = samples;
        _replay = replay;
        _keyboard = keyboard;
        _mode = mode;
        _clock = clock;
    }

    /// <summary>Starts playing on its own thread.</summary>
    public void Start()
    {
        _run = true;
        _lastTicks = _clock.Ticks;
        _thread = new Thread(Loop) { IsBackground = true, Name = "SimulatedKeyboardAudio" };
        _thread.Start();
    }

    private void Loop()
    {
        while (_run)
        {
            Thread.Sleep(StepMs);
            var now = _clock.Ticks;
            var count = (int)Math.Min(ModeInfo.DecoderSampleRate, (now - _lastTicks) * ModeInfo.DecoderSampleRate / TimeSpan.TicksPerSecond);
            _lastTicks = now;
            var audio = AudioFor(_mode());
            if (audio is null || audio.Length == 0 || count <= 0) continue;
            var chunk = new float[count];
            for (var i = 0; i < count; i++)
            {
                chunk[i] = audio[_position];
                _position = (_position + 1) % audio.Length;
            }
            _keyboard.Input.Write(chunk);
        }
    }

    // The recordings for a mode, joined end to end; loaded once.
    private float[]? AudioFor(Mode mode)
    {
        if (mode != _playing)
        {
            _playing = mode;
            _position = 0;
        }
        if (_audio.TryGetValue(mode, out var a)) return a;
        if (ModeInfo.IsSlotted(mode) || FolderFor(mode) is not { } folder) return _audio[mode] = [];
        var list = new List<float>();
        foreach (var f in Directory.EnumerateFiles(folder, "*.wav").Order(StringComparer.Ordinal))
        {
            var (samples, rate) = WavFile.Read(f);
            if (rate == ModeInfo.DecoderSampleRate) list.AddRange(samples.Select(s => s / 32768f));
        }
        return _audio[mode] = [.. list];
    }

    // The mode's recordings: the folder being replayed when it is that mode's folder (samples/rtty) or holds one, else
    // the mode's folder under samples.
    private string? FolderFor(Mode mode)
    {
        var name = ModeInfo.Name(mode).ToLowerInvariant();
        foreach (var root in new[] { _replay, _samples })
        {
            if (root is null || !Directory.Exists(root)) continue;
            if (string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))), name, StringComparison.OrdinalIgnoreCase)) return root;
            var sub = Path.Combine(root, name);
            if (Directory.Exists(sub)) return sub;
        }
        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
    }
}
