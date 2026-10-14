// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Ft8Client.Core;
using Ft8Client.Core.Remote;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Remote;

/// <summary>
/// The rules of remote operation, without the network: pairing, sign-in, who has control, the commands, and the link-loss
/// stop. A phone takes control with any command; operator input at the desk takes it back. While a phone has control the
/// Tx watchdog is capped at <see cref="ControlLease.WatchdogCapMinutes"/> minutes, and when it goes silent for
/// <see cref="ControlLease.LinkTimeout"/> transmit is halted.
/// </summary>
public sealed class RemoteControl
{
    private readonly IRemoteTarget _target;
    private readonly IClock _clock;
    private readonly Action<string> _saveTokens;
    private readonly IRemoteTuning? _tuning;
    private readonly ConcurrentDictionary<string, string> _viewing = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Lowest Tx offset the phone can set, as on the desk.</summary>
    public const int MinOffsetHz = 200;

    /// <summary>Highest Tx offset the phone can set, as on the desk.</summary>
    public const int MaxOffsetHz = 2900;

    /// <summary>Creates the rules over a target session.</summary>
    /// <param name="target">The session.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="tokens">The paired phones.</param>
    /// <param name="saveTokens">Saves the paired phones (to the OS credential store).</param>
    /// <param name="pairing">The pairing code.</param>
    /// <param name="tuning">Band and Tx offset changes; without it the phone cannot change them.</param>
    public RemoteControl(IRemoteTarget target, IClock clock, DeviceTokens tokens, Action<string> saveTokens, PairingGate? pairing = null,
                         IRemoteTuning? tuning = null)
    {
        _target = target;
        _tuning = tuning;
        _clock = clock;
        Tokens = tokens;
        _saveTokens = saveTokens;
        Pairing = pairing ?? new PairingGate();
    }

    /// <summary>Raised when control changes hands, a phone is paired or the paired phones are forgotten.</summary>
    public event Action? Changed;

    /// <summary>Who has control.</summary>
    public ControlLease Lease { get; } = new();

    /// <summary>The paired phones.</summary>
    public DeviceTokens Tokens { get; }

    /// <summary>The pairing code.</summary>
    public PairingGate Pairing { get; }

    /// <summary>A stable id for the phone holding <paramref name="token"/>, or null when the token is not paired.</summary>
    public string? SignIn(string? token)
    {
        if (!Tokens.Verify(token)) return null;
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token!)))[..16];
    }

    /// <summary>Pairs a phone with the code shown in Settings. Returns its token, or null for a wrong or expired code.</summary>
    public string? Pair(string? code)
    {
        if (!Pairing.TryRedeem(code, _clock.UtcNow)) return null;
        var token = Tokens.Issue();
        try
        {
            _saveTokens(Tokens.Serialize());
        }
        catch (Exception ex)
        {
            // The phone is paired until Costas restarts; it will have to pair again after that.
            Serilog.Log.Warning(ex, "Could not save the paired phones");
        }
        Changed?.Invoke();
        return token;
    }

    /// <summary>Forgets every paired phone and hands control back to the desk.</summary>
    public void ForgetPhones()
    {
        Tokens.Clear();
        _saveTokens(string.Empty);
        _viewing.Clear();
        DeskActivity();
        Changed?.Invoke();
    }

    /// <summary>"you", "desk" or "other": who has control, as seen by <paramref name="deviceId"/>.</summary>
    public string ControlFor(string deviceId)
    {
        var holder = Lease.Holder;
        return holder is null ? "desk" : string.Equals(holder, deviceId, StringComparison.Ordinal) ? "you" : "other";
    }

    /// <summary>The station <paramref name="deviceId"/> has open, or null.</summary>
    public string? ViewingFor(string deviceId) => _viewing.TryGetValue(deviceId, out var call) ? call : null;

    /// <summary>Operator input at the desk: the desk is the control point again.</summary>
    public void DeskActivity()
    {
        bool released;
        lock (_gate)
        {
            released = Lease.IsHeld;
            Lease.Release();
        }
        if (!released) return;
        _target.SetWatchdogCap(null);
        Changed?.Invoke();
    }

    /// <summary>Carries out a message from a signed-in phone. Returns a line to show on the phone, or null.</summary>
    public async Task<string?> HandleAsync(string deviceId, RemoteCommand cmd)
    {
        var now = _clock.UtcNow;
        if (cmd.Type == "ping")
        {
            lock (_gate) Lease.Heartbeat(deviceId, now);
            return null;
        }
        if (cmd.Type == "show")
        {
            // Looking at a station changes nothing at the station, so it does not take control.
            var shown = cmd.Call?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(shown)) _viewing.TryRemove(deviceId, out _);
            else _viewing[deviceId] = shown;
            return null;
        }
        var call = cmd.Call?.Trim().ToUpperInvariant() ?? string.Empty;
        // A message the station cannot act on changes nothing: it must not take control or reset the Tx watchdog.
        if (!Understood(cmd, call)) return "The station did not understand that.";
        bool changed;
        lock (_gate)
        {
            changed = !string.Equals(Lease.Holder, deviceId, StringComparison.Ordinal);
            Lease.Take(deviceId, now);
        }
        _target.SetWatchdogCap(ControlLease.WatchdogCapMinutes);
        if (changed) Changed?.Invoke();
        switch (cmd.Type)
        {
            case "halt":
                await _target.HaltAsync(null).ConfigureAwait(false);
                return null;
            case "abandon":
                await _target.AbandonAsync().ConfigureAwait(false);
                return null;
        }
        _target.Activity();
        switch (cmd.Type)
        {
            case "call" when call.Length > 0:
                return _target.CallStation(call) ? null : $"{call} is not in the station list.";
            case "answer" when call.Length > 0:
                _target.AnswerCaller(call);
                return null;
            case "answerAfter" when call.Length > 0:
                _target.AnswerAfterThisContact(call);
                return null;
            case "cq":
                _target.ToggleCq();
                return null;
            case "resend":
                _target.Resend();
                return null;
            case "logNow":
                if (_target.Snapshot.Contact?.CanLogNow == true) _target.LogNow();
                return null;
            case "jump" when cmd.Step is { } step:
                _target.JumpTo(step);
                return null;
            case "band":
                return await BandAsync(cmd).ConfigureAwait(false);
            case "offset" when cmd.Hz is { } hz:
                if (_tuning is null) return "The station cannot change that from the phone.";
                if (hz is < MinOffsetHz or > MaxOffsetHz) return $"The offset must be between {MinOffsetHz} and {MaxOffsetHz} Hz.";
                _tuning.SetTxOffset(hz);
                return null;
            case "offsetAuto":
                if (_tuning is null) return "The station cannot change that from the phone.";
                _tuning.SetTxOffset(null);
                return null;
            default:
                return "The station did not understand that.";
        }
    }

    private static bool Understood(RemoteCommand cmd, string call) => cmd.Type switch
    {
        "halt" or "abandon" or "cq" or "resend" or "logNow" or "band" or "offsetAuto" => true,
        "call" or "answer" or "answerAfter" => call.Length > 0,
        "jump" => cmd.Step is not null,
        "offset" => cmd.Hz is not null,
        _ => false,
    };

    // As the band menu on the desk: not while transmitting, and only to a band in the frequency table.
    private async Task<string?> BandAsync(RemoteCommand cmd)
    {
        if (_tuning is null) return "The station cannot change that from the phone.";
        if (_target.Snapshot.Transmitting) return "Wait until this transmission ends to change band.";
        var mode = _target.Snapshot.Mode;
        if (cmd.Mode is { Length: > 0 } m && !ModeInfo.TryParse(m, out mode)) return $"{m} is not a mode the station knows.";
        var bands = _tuning.Bands(mode);
        var band = cmd.Band?.Trim().Replace(" ", string.Empty, StringComparison.Ordinal) ?? _target.Snapshot.Band;
        if (!bands.Contains(band, StringComparer.OrdinalIgnoreCase))
        {
            // A mode change keeps the band when it can, like the desk; otherwise the first band of that mode.
            if (cmd.Band is { Length: > 0 } && cmd.Mode is null) return $"{cmd.Band} is not in the frequency table for {ModeInfo.Name(mode)}.";
            if (bands.Count == 0) return $"There are no {ModeInfo.Name(mode)} frequencies in the frequency table.";
            band = bands[0];
        }
        else
        {
            band = bands.First(b => string.Equals(b, band, StringComparison.OrdinalIgnoreCase));
        }
        await _tuning.SetBandAsync(band, mode).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Checks the link of the phone in control. When it has been silent too long, control returns to the desk and transmit
    /// is halted, with a line saying why if anything was going out. Call about once a second.
    /// </summary>
    public async Task TickAsync()
    {
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (!Lease.LinkLost(now)) return;
            Lease.Release();
        }
        var wanted = _target.WantsTransmit;
        _target.SetWatchdogCap(null);
        await _target.HaltAsync(wanted
            ? $"Transmit stopped at {now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}: lost touch with the phone."
            : null).ConfigureAwait(false);
        Changed?.Invoke();
    }
}
