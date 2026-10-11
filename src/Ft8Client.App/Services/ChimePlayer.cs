// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Dsp;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Plays the new-caller chime on the computer's default speakers. It refuses when the default device is the radio's
/// output or no radio output is set, so the chime can never reach the transmitter (a VOX rig would key on it).
/// </summary>
public sealed class ChimePlayer(Func<IAudioBackend> backend, Func<string?> radioOutputId)
{
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(5);
    private DateTime _lastUtc = DateTime.MinValue;
    private int _playing;

    /// <summary>
    /// Returns the device the chime may play on, or null: only the default device, and only when it is known to differ
    /// from the radio's output.
    /// </summary>
    public static string? SafeDevice(string? defaultOutputId, string? radioOutputId) =>
        defaultOutputId is null || radioOutputId is null || string.Equals(defaultOutputId, radioOutputId, StringComparison.Ordinal)
            ? null
            : defaultOutputId;

    /// <summary>Plays the chime in the background, at most once every five seconds.</summary>
    public void Play(DateTime nowUtc)
    {
        if (nowUtc - _lastUtc < MinGap || Interlocked.Exchange(ref _playing, 1) == 1) return;
        _lastUtc = nowUtc;
        var b = backend();
        _ = Task.Run(async () =>
        {
            try
            {
                if (SafeDevice(b.DefaultOutputId(), radioOutputId()) is not { } id)
                {
                    Log.Information("New-caller chime skipped: the default speakers are the radio's audio device, or no radio audio device is set");
                    return;
                }
                using var output = b.OpenOutput(id, 48_000);
                output.Play(ChimeTone.Render(output.SampleRate));
                for (var i = 0; i < 40 && output.IsPlaying; i++) await Task.Delay(50).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false); // let the device drain before closing it
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not play the new-caller chime");
            }
            finally
            {
                Volatile.Write(ref _playing, 0);
            }
        });
    }
}
