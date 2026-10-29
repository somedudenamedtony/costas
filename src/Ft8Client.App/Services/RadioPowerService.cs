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
/// The radio's transmit power setting, for the desk's power box and the phone. Reads it every few seconds between
/// transmissions and changes it on request. Only the setting: nothing here keys the radio, and no change is made while
/// a transmission or tune is on the air.
/// </summary>
public sealed class RadioPowerService : IAsyncDisposable
{
    /// <summary>Lowest power that can be asked for; the radio may raise it to its own minimum.</summary>
    public const int MinWatts = 1;

    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Func<IRig> _rig;
    private readonly Func<bool> _transmitting;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task _loop = Task.CompletedTask;
    private RigPower? _power;

    /// <summary>Creates the service over whichever radio is open now.</summary>
    /// <param name="rig">The radio open now.</param>
    /// <param name="transmitting">True while anything is on the air (PTT, a transmission or a tune).</param>
    public RadioPowerService(Func<IRig> rig, Func<bool> transmitting)
    {
        _rig = rig;
        _transmitting = transmitting;
    }

    /// <summary>Raised when the power setting read from the radio changes, or stops being readable.</summary>
    public event Action? Changed;

    /// <summary>The power setting as last read, or null when the radio does not report it in watts.</summary>
    public RigPower? Power => Volatile.Read(ref _power);

    /// <summary>False while transmitting.</summary>
    public bool CanChange => !_transmitting();

    /// <summary>Starts reading the power every few seconds.</summary>
    public void Start() => _loop = Task.Run(LoopAsync);

    /// <summary>Reads the power from the radio now, unless something is on the air.</summary>
    public async Task RefreshAsync()
    {
        if (_transmitting() || !await _busy.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            Update(await _rig().ReadPowerAsync(cts.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // No answer in time: keep the last reading and try again next time.
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>
    /// Changes the power setting. Returns null when the radio took it exactly, else one line saying why it was refused
    /// or what the radio set instead. Anything outside 1 W to the radio's maximum is refused, never rounded.
    /// </summary>
    public async Task<string?> SetAsync(int watts)
    {
        if (Power is not { } now) return "The radio does not report its power, so Costas cannot change it.";
        if (_transmitting()) return "Wait until this transmission ends to change power.";
        if (watts < MinWatts || watts > now.MaxWatts) return $"Power must be between {MinWatts} and {now.MaxWatts} W.";
        await _busy.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_transmitting()) return "Wait until this transmission ends to change power.";
            using var cts = new CancellationTokenSource(Timeout);
            RigPower? set;
            try
            {
                set = await _rig().SetPowerWattsAsync(watts, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                set = null;
            }
            Log.Information("Radio power set to {Watts} W; radio reads {Read}", watts, set?.Watts);
            if (set is null) return "The radio did not take the power change.";
            Update(set);
            return set.Watts == watts ? null : $"The radio set {set.Watts} W.";
        }
        finally
        {
            _busy.Release();
        }
    }

    private void Update(RigPower? p)
    {
        if (Interlocked.Exchange(ref _power, p) != p) Changed?.Invoke();
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync().ConfigureAwait(false);
                await Task.Delay(Every, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Reading radio power");
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
