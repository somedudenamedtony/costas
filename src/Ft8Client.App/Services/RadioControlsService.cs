// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Rig;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// The radio's receive and tuner settings (gains, preamp, attenuator, AGC, noise blanker, noise reduction, tuner in
/// line), read over CAT while someone is looking at them and changed on request. Like <see cref="RadioPowerService"/>,
/// it never talks to the radio while anything is on the air, and changing a setting never keys the radio.
/// </summary>
public sealed class RadioControlsService : IAsyncDisposable
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Func<IRig> _rig;
    private readonly Func<bool> _transmitting;
    private readonly Func<bool> _wanted;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task _loop = Task.CompletedTask;
    private IReadOnlyList<RigControlValue> _controls = [];

    /// <summary>Creates the service over whichever radio is open now.</summary>
    /// <param name="rig">The radio open now.</param>
    /// <param name="transmitting">True while anything is on the air (PTT, a transmission or a tune).</param>
    /// <param name="wanted">True while someone is looking at the settings; they are only read then.</param>
    public RadioControlsService(Func<IRig> rig, Func<bool> transmitting, Func<bool> wanted)
    {
        _rig = rig;
        _transmitting = transmitting;
        _wanted = wanted;
    }

    /// <summary>Raised when a setting read from the radio changes.</summary>
    public event Action? Changed;

    /// <summary>The settings as last read; empty when the radio reports none or they have not been read yet.</summary>
    public IReadOnlyList<RigControlValue> Controls => Volatile.Read(ref _controls);

    /// <summary>False while transmitting.</summary>
    public bool CanChange => !_transmitting();

    /// <summary>Starts reading the settings every few seconds while they are wanted.</summary>
    public void Start() => _loop = Task.Run(LoopAsync);

    /// <summary>Reads the settings from the radio now, unless something is on the air.</summary>
    public async Task RefreshAsync()
    {
        if (_transmitting() || !await _busy.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            await ReadAsync().ConfigureAwait(false);
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>
    /// Changes one setting. Returns null when the radio took it, else one line saying why not. A value the radio did
    /// not list is refused, never rounded to the nearest one.
    /// </summary>
    public async Task<string?> SetAsync(RigControl control, int value)
    {
        if (Controls.FirstOrDefault(c => c.Control == control) is not { } now) return "The radio does not report that setting.";
        if (_transmitting()) return "Wait until this transmission ends to change the radio's settings.";
        if (now.Options.Count > 0 ? !now.Options.Contains(value) : !InRange(control, value)) return "The radio does not have that setting.";
        await _busy.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_transmitting()) return "Wait until this transmission ends to change the radio's settings.";
            bool ok;
            using (var cts = new CancellationTokenSource(Timeout))
            {
                try
                {
                    ok = await _rig().SetControlAsync(control, value, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ok = false;
                }
            }
            Log.Information("Radio {Control} set to {Value}: {Result}", control, value, ok ? "taken" : "refused");
            await ReadAsync().ConfigureAwait(false);
            return ok ? null : "The radio did not take that change.";
        }
        finally
        {
            _busy.Release();
        }
    }

    private static bool InRange(RigControl control, int value) =>
        control is RigControl.AfGain or RigControl.RfGain ? value is >= 0 and <= 100 : value is 0 or 1;

    private async Task ReadAsync()
    {
        IReadOnlyList<RigControlValue> read;
        using (var cts = new CancellationTokenSource(Timeout))
        {
            try
            {
                read = await _rig().ReadControlsAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // No answer in time: keep the last reading and try again next time.
                return;
            }
        }
        var old = Interlocked.Exchange(ref _controls, read);
        if (!Same(old, read)) Changed?.Invoke();
    }

    private static bool Same(IReadOnlyList<RigControlValue> a, IReadOnlyList<RigControlValue> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Control == p.Second.Control && p.First.Value == p.Second.Value
                                                && p.First.Options.SequenceEqual(p.Second.Options));

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_wanted()) await RefreshAsync().ConfigureAwait(false);
                await Task.Delay(Every, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Reading radio settings");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _cts.Dispose();
    }
}
