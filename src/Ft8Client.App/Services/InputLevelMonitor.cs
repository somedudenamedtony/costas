// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Dsp;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Services;

/// <summary>
/// Setup's input meter: opens the chosen capture device (shared mode, alongside the app's own capture) and reads its
/// ring buffer ten times a second on a background task, never in the audio callback.
/// </summary>
public sealed class InputLevelMonitor : IDisposable
{
    private readonly IAudioInput _input;
    private readonly IClock _clock;
    private readonly Action<InputLevelReading> _onUpdate;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private volatile string? _stopped;

    private InputLevelMonitor(IAudioInput input, IClock clock, Action<InputLevelReading> onUpdate)
    {
        _input = input;
        _clock = clock;
        _onUpdate = onUpdate;
        _input.Stopped += (_, why) => _stopped = why;
        _input.Start();
        _loop = Task.Run(LoopAsync);
    }

    /// <summary>
    /// Opens <paramref name="deviceId"/> and starts reporting to <paramref name="onUpdate"/> (called off the UI
    /// thread). Throws what the backend throws when the device cannot be opened.
    /// </summary>
    public static InputLevelMonitor Start(IAudioBackend backend, string deviceId, IClock clock, Action<InputLevelReading> onUpdate) =>
        new(backend.OpenInput(deviceId, 48_000), clock, onUpdate);

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }
        _input.Dispose();
        _cts.Dispose();
    }

    private async Task LoopAsync()
    {
        var meter = new InputLevelMeter(_clock.UtcNow);
        var block = new float[Math.Max(_input.SampleRate / 2, 1024)];
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
            {
                // Keep only the newest block when more is waiting than one block holds.
                int n, last = 0;
                while ((n = _input.Buffer.Read(block)) > 0) last = n;
                meter.Update(block.AsSpan(0, last), _clock.UtcNow);
                _onUpdate(new InputLevelReading(meter.RmsDbfs, meter.State, meter.Passed, _stopped));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
