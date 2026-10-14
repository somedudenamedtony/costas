// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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
    private DateTime? _lateAlignUtc;
    private TxHold? _hold;
    private DateTime? _lastStartedSlot;
    private bool _ptt;
    private AudioStream? _stream;
    private DateTime? _streamAudioDueUtc;
    private bool _streamPlaying;
    private Mode _streamMode;
    private int _streamOffsetHz;
    private int? _streamWidthHz;

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

    /// <summary>Raised when a Tune or test tone starts.</summary>
    public event Action<PreparedTx>? ToneStarted;

    /// <summary>Raised when a transmission ends; true if it completed, false if it was cut short.</summary>
    public event Action<PreparedTx, bool>? Ended;

    /// <summary>Raised when a keyboard-mode over's audio starts.</summary>
    public event Action? StreamStarted;

    /// <summary>Raised when a keyboard-mode over ends; true if it played to its end, false if it was cut short.</summary>
    public event Action<bool>? StreamEnded;

    /// <summary>Raised with a fault message after PTT has been released.</summary>
    public event Action<string>? Fault;

    /// <summary>Transmit gain in dBFS.</summary>
    public double GainDb { get; set; } = -6.0;

    /// <summary>
    /// Why this build does not transmit at all, or null. Every transmission, Tune and tuner start is refused with it
    /// before PTT is touched.
    /// </summary>
    public string? DisabledReason { get; set; }

    /// <summary>A start later than this after its mark is skipped.</summary>
    public TimeSpan LateStartLimit { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>True while PTT is asserted or audio is playing.</summary>
    public bool Transmitting
    {
        get { lock (_gate) return _ptt || _current is not null || _stream is not null; }
    }

    /// <summary>The output's sample rate, for rendering audio ahead (48 kHz when no output is open).</summary>
    public int OutputSampleRate => _output?.SampleRate ?? 48_000;

    /// <summary>
    /// Starts a keyboard-mode over (docs/09-keyboard-modes.md section 9): PTT now, the stream's audio after the PTT lead,
    /// and PTT off a tail after the stream finishes. Returns null once started, or why it was refused: transmitting is
    /// disabled, no audio output, a radio fault, outside the band, already transmitting, or PTT could not be asserted.
    /// The clock check does not apply, since these modes have no slots. Rig and audio faults, and Halt, end it like any
    /// transmission. <paramref name="widthHz"/> is the signal's width for the band-edge check (an RTTY shift wider than
    /// the standard), or null for the mode's usual width.
    /// </summary>
    public string? StartStream(AudioStream stream, Mode mode, int offsetHz, TxGuardInput guard, TimeSpan pttLead, int? widthHz = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(guard);
        if (DisabledReason is not null) return DisabledReason;
        if (!guard.AudioReady || _output is null) return "No audio output device is open; transmitting is disabled.";
        if (guard.RigFault is not null) return $"Radio fault: {guard.RigFault}";
        if (!_frequencies.TransmitAllowed(guard.Band, mode, guard.DialHz, offsetHz, widthHz, out var bandWhy)) return bandWhy;
        lock (_gate)
        {
            if (_ptt || _current is not null || _next is not null || _stream is not null) return "Already transmitting.";
            _stream = stream;
            _streamPlaying = false;
            _streamMode = mode;
            _streamOffsetHz = offsetHz;
            _streamWidthHz = widthHz;
            _ptt = true;
            _streamAudioDueUtc = _clock.UtcNow + pttLead;
        }
        var ptt = SetPttSafe(true);
        // A radio that refuses PTT at once has already halted the over: say why instead of reporting it started.
        if (ptt.IsCompletedSuccessfully && ptt.Result is { } failed) return failed;
        lock (_gate)
        {
            if (!ReferenceEquals(_stream, stream)) return "The over was stopped before it started.";
        }
        return null;
    }

    /// <summary>True while <paramref name="stream"/> is the keyboard over this transmitter is sending.</summary>
    public bool IsStreaming(AudioStream stream)
    {
        lock (_gate) return _stream is not null && ReferenceEquals(_stream, stream);
    }

    /// <summary>
    /// Checks what is on the air now against the band-edge limits at <paramref name="dialHz"/>. Returns null when
    /// nothing is on the air or it still fits, otherwise why it must stop (the dial moved during a transmission).
    /// </summary>
    public string? OnAirOutsideBand(string band, long dialHz)
    {
        Mode mode;
        int offset;
        int? width;
        lock (_gate)
        {
            if (_stream is not null)
            {
                (mode, offset, width) = (_streamMode, _streamOffsetHz, _streamWidthHz);
            }
            else if ((_current ?? (_ptt ? _next : null)) is { } tx)
            {
                // A Tune tone is checked as FT8 is when it starts.
                (mode, offset, width) = (tx.Mode, tx.OffsetHz, null);
            }
            else
            {
                return null;
            }
        }
        return _frequencies.TransmitAllowed(band, mode, dialHz, offset, width, out var why) ? null : why;
    }

    /// <summary>The message on the air now, or null.</summary>
    public string? CurrentMessage
    {
        get { lock (_gate) return _current?.Plan.Message; }
    }

    /// <summary>The transmission waiting for its slot.</summary>
    public PreparedTx? Pending
    {
        get { lock (_gate) return _next; }
    }

    /// <summary>True while the pending transmission is held back waiting for decodes.</summary>
    public bool Held
    {
        get { lock (_gate) return _hold is not null && _next is not null; }
    }

    /// <summary>True when audio for the slot has started (or the slot's transmission has finished).</summary>
    public bool AudioStarted(DateTime slotStartUtc)
    {
        lock (_gate) return (_current is { } c && c.Plan.SlotStartUtc == slotStartUtc) || _lastStartedSlot == slotStartUtc;
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
    /// With a <paramref name="hold"/>, the slot's PTT and audio marks are skipped until <see cref="Release"/> or a new
    /// <see cref="Prepare"/>; if neither comes before the hold expires, the transmission starts late by itself.
    /// </summary>
    public PreparedTx? Prepare(TxPlan plan, Mode mode, int offsetHz, TxGuardInput guard, out string? refusal, TxHold? hold = null)
    {
        refusal = DisabledReason;
        if (refusal is not null) return null;
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
        lock (_gate)
        {
            _next = tx;
            _hold = hold;
        }
        return tx;
    }

    /// <summary>
    /// Lifts the hold on the pending transmission. Returns false when nothing was held. The caller starts it with
    /// <see cref="BeginLate"/> if its PTT mark has passed.
    /// </summary>
    public bool Release()
    {
        lock (_gate)
        {
            if (_hold is null || _next is null) return false;
            _hold = null;
            return true;
        }
    }

    /// <summary>Drops a prepared transmission that has not started, releasing PTT if it was asserted for it.</summary>
    public void CancelPending()
    {
        lock (_gate)
        {
            _next = null;
            _hold = null;
            _lateAudioDueUtc = null;
            _lateAlignUtc = null;
            if (_current is null && _ptt) _pttOffDueUtc = _clock.UtcNow;
        }
    }

    /// <summary>Slot clock events. Called on the slot thread; never waits on I/O.</summary>
    public void OnSlotEvent(SlotEvent e)
    {
        PreparedTx? tx;
        lock (_gate)
        {
            tx = _next is { } n && n.Plan.SlotStartUtc == e.SlotStartUtc ? n : null;
            // Held for decodes, or already started late: Poll starts the audio.
            if (_hold is not null || _lateAudioDueUtc is not null) tx = null;
        }
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
                    _lastStartedSlot = tx.Plan.SlotStartUtc;
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
    /// Starts a prepared transmission after its marks have passed (decodes arrived late, or the operator acted late): PTT
    /// now, audio after the PTT lead. Without <paramref name="alignToUtc"/> the whole message is sent, shifted later.
    /// With it, the message keeps its place in the slot as if it had started at <paramref name="alignToUtc"/> (the Tx
    /// start) and the part that would have been sent already is left out, as WSJT-X does for a late start. The caller
    /// has checked that the start is within the limit.
    /// </summary>
    public void BeginLate(PreparedTx tx, TimeSpan pttLead, DateTime? alignToUtc = null)
    {
        lock (_gate)
        {
            _next = tx;
            _ptt = true;
            _lateAudioDueUtc = _clock.UtcNow + pttLead;
            _lateAlignUtc = alignToUtc;
        }
        _ = SetPttSafe(true);
    }
    /// <summary>Releases PTT when the audio and its tail have finished. Called on the slot thread every millisecond.</summary>
    public void Poll()
    {
        PollStream();
        var keyHeld = false;
        lock (_gate)
        {
            if (_hold is { } h && _next is not null && _clock.UtcNow >= h.UntilUtc)
            {
                // The decodes did not arrive in time: send what was prepared, late but within the limit.
                _hold = null;
                _ptt = true;
                _lateAudioDueUtc = _clock.UtcNow + h.PttLead;
                keyHeld = true;
            }
        }
        if (keyHeld) _ = SetPttSafe(true);

        PreparedTx? late = null;
        lock (_gate)
        {
            if (_lateAudioDueUtc is { } la && _clock.UtcNow >= la && _next is { } n && _ptt)
            {
                _lateAudioDueUtc = null;
                _next = null;
                if (_lateAlignUtc is { } align)
                {
                    // Leave out what would already have been sent; nothing left means nothing to send.
                    var skip = (int)Math.Round((_clock.UtcNow - align).TotalSeconds * n.SampleRate);
                    n = skip <= 0 ? n : n with { Samples = skip < n.Samples.Length ? n.Samples[skip..] : [] };
                    _lateAlignUtc = null;
                }
                _current = n;
                _lastStartedSlot = n.Plan.SlotStartUtc;
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
        var streamDone = false;
        lock (_gate)
        {
            if (_pttOffDueUtc is not { } due || _clock.UtcNow < due) return;
            _pttOffDueUtc = null;
            _ptt = false;
            done = _current;
            _current = null;
            if (_stream is not null && _streamPlaying)
            {
                _stream = null;
                _streamPlaying = false;
                streamDone = true;
            }
        }
        _ = SetPttSafe(false);
        if (done is not null) Ended?.Invoke(done, true);
        if (streamDone) StreamEnded?.Invoke(true);
    }

    // A keyboard over: audio once the PTT lead has passed, and the PTT tail once the stream has played out.
    private void PollStream()
    {
        AudioStream? start = null;
        lock (_gate)
        {
            if (_stream is null) return;
            var now = _clock.UtcNow;
            if (_streamAudioDueUtc is { } due)
            {
                if (now < due || !_ptt) return;
                _streamAudioDueUtc = null;
                _streamPlaying = true;
                start = _stream;
            }
            else if (_streamPlaying && _pttOffDueUtc is null && _stream.Finished && _output?.IsPlaying != true)
            {
                _pttOffDueUtc = now + Tail;
            }
        }
        if (start is null) return;
        _output!.PlayStream(start);
        StreamStarted?.Invoke();
    }

    /// <summary>Halt: stops audio and drops PTT immediately.</summary>
    public async Task HaltAsync()
    {
        PreparedTx? cut;
        bool streamCut;
        lock (_gate)
        {
            streamCut = _stream is not null;
            _stream = null;
            _streamAudioDueUtc = null;
            _streamPlaying = false;
            cut = _current;
            _current = null;
            _next = null;
            _hold = null;
            _pttOffDueUtc = null;
            _lateAudioDueUtc = null;
            _lateAlignUtc = null;
            _ptt = false;
        }
        _output?.Stop();
        await SetPttSafe(false).ConfigureAwait(false);
        if (cut is not null) Ended?.Invoke(cut, false);
        if (streamCut) StreamEnded?.Invoke(false);
    }

    /// <summary>
    /// Starts the radio's own antenna tuner, after the same radio and band-edge guards as a tone. True when the radio
    /// accepted it; false when the radio has no tuner command, refused it, or a guard refuses (the caller then sends a
    /// tune tone, whose guards say why).
    /// </summary>
    public async Task<bool> StartRigTunerAsync(int offsetHz, TxGuardInput guard)
    {
        if (DisabledReason is not null || guard.RigFault is not null) return false;
        if (!_frequencies.TransmitAllowed(guard.Band, Mode.Ft8, guard.DialHz, offsetHz, out _)) return false;
        return await _rig.StartTunerAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Tune: a steady tone at the offset for up to <paramref name="seconds"/> (the Tune watchdog), PTT on now. Returns
    /// null once the tone is on the air, or why it was refused or stopped: no audio output, a radio fault or no radio,
    /// outside the band, or PTT could not be asserted (then no audio is played).
    /// </summary>
    public async Task<string?> TuneAsync(int offsetHz, double seconds, TxGuardInput guard)
    {
        if (DisabledReason is not null) return DisabledReason;
        if (!guard.AudioReady || _output is null) return "No audio output device is open.";
        if (guard.RigFault is not null) return $"Radio fault: {guard.RigFault}";
        if (!_frequencies.TransmitAllowed(guard.Band, Mode.Ft8, guard.DialHz, offsetHz, out var why)) return why;
        var n = (int)(seconds * _output.SampleRate);
        var tone = new float[n];
        var amp = Math.Pow(10, GainDb / 20);
        for (var i = 0; i < n; i++) tone[i] = (float)(amp * Math.Sin(2 * Math.PI * (offsetHz + 25) * i / _output.SampleRate));
        var tx = new PreparedTx(new TxPlan(_clock.UtcNow, Session.TuneMessage, null, null), Mode.Ft8, offsetHz, tone, _output.SampleRate);
        lock (_gate)
        {
            _ptt = true;
            _current = tx;
            _pttOffDueUtc = _clock.UtcNow + tx.Duration + Tail;
        }
        var failed = await SetPttSafe(true).ConfigureAwait(false);
        if (failed is not null) return failed; // halted: PTT released, nothing to play
        lock (_gate)
        {
            if (!ReferenceEquals(_current, tx)) return "The tone was stopped before it started."; // halted meanwhile
        }
        _output.Play(tone);
        ToneStarted?.Invoke(tx);
        return null;
    }

    /// <summary>Sets PTT. When asserting fails, halts and returns why (also raised as <see cref="Fault"/>).</summary>
    private async Task<string?> SetPttSafe(bool on)
    {
        try
        {
            await _rig.SetPttAsync(on, CancellationToken.None).ConfigureAwait(false);
            return null;
        }
        catch (RigException ex)
        {
            if (!on)
            {
                Fault?.Invoke(ex.Message);
                return ex.Message;
            }
            var why = $"PTT could not be asserted: {ex.Message}";
            await AbortAsync(why).ConfigureAwait(false);
            return why;
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

    /// <summary>Stops the transmission like Halt, then reports <paramref name="message"/> as a fault.</summary>
    public async Task AbortAsync(string message)
    {
        await HaltAsync().ConfigureAwait(false);
        Fault?.Invoke(message);
    }
}
