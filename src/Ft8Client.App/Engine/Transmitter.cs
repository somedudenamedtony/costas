// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Encoding;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Rig;

namespace Ft8Client.App.Engine;

/// <summary>
/// Keys the radio and plays pre-rendered audio on the slot clock: PTT at the PttOn mark (Tx start minus the lead),
/// audio at the TxStart mark, PTT off a 100 ms tail after the audio ends. Any rig or audio fault during a
/// transmission stops the audio and releases PTT first, then reports. Guards (encoding, band edge, clock, devices)
/// refuse a transmission before it starts. Slot-thread entry points never wait on I/O.
/// </summary>
public sealed class Transmitter
{
    /// <summary>PTT tail after the audio ends.</summary>
    public static readonly TimeSpan Tail = TimeSpan.FromMilliseconds(100);

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly FrequencyTable _frequencies;
    private IRig _rig;
    private IAudioOutput? _output;
    private PreparedTx? _next;
    private PreparedTx? _current;
    private DateTime _audioStartedUtc;
    private DateTime? _pttOffDueUtc;
    private DateTime? _lateAudioDueUtc;
    private bool _ptt;

    /// <summary>Creates a transmitter.</summary>
    public Transmitter(IRig rig, IAudioOutput? output, IClock clock, FrequencyTable frequencies)
    {
        _rig = rig;
        _output = output;
        _clock = clock;
        _frequencies = frequencies;
        _rig.Faulted += OnRigFault;
        if (_output is not null) _output.Faulted += OnAudioFault;
    }

    /// <summary>Raised when audio starts (plan, start time).</summary>
    public event Action<PreparedTx, DateTime>? Started;

    /// <summary>Raised when a transmission ends; true if it completed, false if it was cut short.</summary>
    public event Action<PreparedTx, bool>? Ended;

    /// <summary>Raised with a fault message after PTT has been released.</summary>
    public event Action<string>? Fault;

    /// <summary>Transmit gain in dBFS.</summary>
    public double GainDb { get; set; } = -6.0;

    /// <summary>A start later than this after its mark is skipped.</summary>
    public TimeSpan LateStartLimit { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>True while PTT is asserted or audio is playing.</summary>
    public bool Transmitting
    {
        get { lock (_gate) return _ptt || _current is not null; }
    }

    /// <summary>The transmission waiting for its slot.</summary>
    public PreparedTx? Pending
    {
        get { lock (_gate) return _next; }
    }

    /// <summary>Replaces the rig (after setup or a profile change).</summary>
    public void SetRig(IRig rig)
    {
        _rig.Faulted -= OnRigFault;
        _rig = rig;
        _rig.Faulted += OnRigFault;
    }

    /// <summary>Replaces the audio output.</summary>
    public void SetOutput(IAudioOutput? output)
    {
        if (_output is not null) _output.Faulted -= OnAudioFault;
        _output = output;
        if (_output is not null) _output.Faulted += OnAudioFault;
    }

    /// <summary>
    /// Checks and renders a transmission for its slot. Returns null and a reason when it must not go out: the message
    /// cannot be sent exactly as shown, it would fall outside the band, the clock is off, or a device is missing.
    /// </summary>
    public PreparedTx? Prepare(TxPlan plan, Mode mode, int offsetHz, TxGuardInput guard, out string? refusal)
    {
        refusal = null;
        if (!guard.AudioReady || _output is null)
        {
            refusal = "No audio output device is open; transmitting is disabled.";
            return null;
        }
        if (guard.RigFault is not null)
        {
            refusal = $"Radio fault: {guard.RigFault}";
            return null;
        }
        if (!ClockEstimate.TransmitAllowed(guard.ClockOffsetSeconds, guard.ClockBlockSeconds, out var clockWhy))
        {
            refusal = clockWhy;
            return null;
        }
        if (!_frequencies.TransmitAllowed(guard.Band, mode, guard.DialHz, offsetHz, out var bandWhy))
        {
            refusal = bandWhy;
            return null;
        }
        var enc = FtxEncoder.Encode(plan.Message, mode);
        if (!enc.Ok)
        {
            refusal = $"Not sent: \"{plan.Message}\". {enc.Error}";
            return null;
        }
        var samples = GfskSynth.Render(enc.Tones, mode, offsetHz, _output.SampleRate, Math.Pow(10, GainDb / 20));
        var tx = new PreparedTx(plan, mode, offsetHz, samples, _output.SampleRate);
        lock (_gate) _next = tx;
        return tx;
    }

    /// <summary>Drops a prepared transmission that has not started.</summary>
    public void CancelPending()
    {
        lock (_gate) _next = null;
    }

    /// <summary>Slot clock events. Called on the slot thread; never waits on I/O.</summary>
    public void OnSlotEvent(SlotEvent e)
    {
        PreparedTx? tx;
        lock (_gate) tx = _next is { } n && n.Plan.SlotStartUtc == e.SlotStartUtc ? n : null;
        if (tx is null) return;

        switch (e.Mark)
        {
            case SlotMark.PttOn when e.LateBy <= LateStartLimit:
                lock (_gate) _ptt = true;
                _ = SetPttSafe(true);
                break;
            case SlotMark.TxStart when e.LateBy <= LateStartLimit:
                lock (_gate)
                {
                    if (!_ptt) return; // PTT was not asserted (fault or too late): do not play
                    _next = null;
                    _current = tx;
                    _audioStartedUtc = _clock.UtcNow;
                    _pttOffDueUtc = _audioStartedUtc + tx.Duration + Tail;
                }
                _output!.Play(tx.Samples);
                Started?.Invoke(tx, _audioStartedUtc);
                break;
            case SlotMark.PttOn or SlotMark.TxStart:
                // Too late to start this slot: skip it.
                lock (_gate)
                {
                    _next = null;
                    if (_current is null && _ptt) _pttOffDueUtc = _clock.UtcNow;
                }
                Poll();
                break;
        }
    }

    /// <summary>
    /// Starts a prepared transmission after its marks have passed (decodes arrived late): PTT now, audio after the
    /// PTT lead. The caller has checked that the start is within the late-start limit.
    /// </summary>
    public void BeginLate(PreparedTx tx, TimeSpan pttLead)
    {
        lock (_gate)
        {
            _next = tx;
            _ptt = true;
            _lateAudioDueUtc = _clock.UtcNow + pttLead;
        }
        _ = SetPttSafe(true);
    }

    /// <summary>Releases PTT when the audio and its tail have finished. Called on the slot thread every millisecond.</summary>
    public void Poll()
    {
        PreparedTx? late = null;
        lock (_gate)
        {
            if (_lateAudioDueUtc is { } la && _clock.UtcNow >= la && _next is { } n && _ptt)
            {
                _lateAudioDueUtc = null;
                _next = null;
                _current = n;
                _audioStartedUtc = _clock.UtcNow;
                _pttOffDueUtc = _audioStartedUtc + n.Duration + Tail;
                late = n;
            }
        }
        if (late is not null)
        {
            _output!.Play(late.Samples);
            Started?.Invoke(late, _audioStartedUtc);
        }

        PreparedTx? done = null;
        lock (_gate)
        {
            if (_pttOffDueUtc is not { } due || _clock.UtcNow < due) return;
            _pttOffDueUtc = null;
            _ptt = false;
            done = _current;
            _current = null;
        }
        _ = SetPttSafe(false);
        if (done is not null) Ended?.Invoke(done, true);
    }

    /// <summary>Halt: stops audio and drops PTT immediately.</summary>
    public async Task HaltAsync()
    {
        PreparedTx? cut;
        lock (_gate)
        {
            cut = _current;
            _current = null;
            _next = null;
            _pttOffDueUtc = null;
            _lateAudioDueUtc = null;
            _ptt = false;
        }
        _output?.Stop();
        await SetPttSafe(false).ConfigureAwait(false);
        if (cut is not null) Ended?.Invoke(cut, false);
    }

    /// <summary>Tune: a steady tone at the offset for up to <paramref name="seconds"/> (the Tune watchdog), PTT on now.</summary>
    public async Task<string?> TuneAsync(int offsetHz, double seconds, TxGuardInput guard)
    {
        if (!guard.AudioReady || _output is null) return "No audio output device is open.";
        if (!_frequencies.TransmitAllowed(guard.Band, Mode.Ft8, guard.DialHz, offsetHz, out var why)) return why;
        var n = (int)(seconds * _output.SampleRate);
        var tone = new float[n];
        var amp = Math.Pow(10, GainDb / 20);
        for (var i = 0; i < n; i++) tone[i] = (float)(amp * Math.Sin(2 * Math.PI * (offsetHz + 25) * i / _output.SampleRate));
        var tx = new PreparedTx(new TxPlan(_clock.UtcNow, "Tune", null, null), Mode.Ft8, offsetHz, tone, _output.SampleRate);
        lock (_gate)
        {
            _ptt = true;
            _current = tx;
            _pttOffDueUtc = _clock.UtcNow + tx.Duration + Tail;
        }
        await SetPttSafe(true).ConfigureAwait(false);
        _output.Play(tone);
        return null;
    }

    private async Task SetPttSafe(bool on)
    {
        try
        {
            await _rig.SetPttAsync(on, CancellationToken.None).ConfigureAwait(false);
        }
        catch (RigException ex)
        {
            if (on) await AbortAsync($"PTT could not be asserted: {ex.Message}").ConfigureAwait(false);
            else Fault?.Invoke(ex.Message);
        }
    }

    private void OnRigFault(object? sender, RigFault f)
    {
        if (Transmitting) _ = AbortAsync($"Radio fault during transmission: {f.Message}");
    }

    private void OnAudioFault(object? sender, string why)
    {
        if (Transmitting) _ = AbortAsync($"Audio output fault during transmission: {why}");
    }

    private async Task AbortAsync(string message)
    {
        await HaltAsync().ConfigureAwait(false);
        Fault?.Invoke(message);
    }
}
