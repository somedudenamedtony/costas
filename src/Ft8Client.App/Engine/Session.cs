// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;
using Ft8Client.Core.Transmit;

namespace Ft8Client.App.Engine;

/// <summary>
/// The orchestrator: slot clock events in, decodes through the tracker, contact engine and ranker, transmissions
/// out through the transmitter, immutable snapshots to the UI. Headless; the UI only renders snapshots and sends
/// commands. State changes happen under one lock; decoding and I/O happen outside it.
/// </summary>
public sealed class Session : Ft8Client.App.Remote.IRemoteTarget
{
    private const int MaxRaw = 1500;
    private const int QuickSpanHz = 100;
    private readonly object _gate = new();
    private readonly IDecoder _decoder;
    private readonly ISlotSource _source;
    private readonly Transmitter _tx;
    private readonly IClock _clock;
    private readonly CountryFile _countries;
    private readonly FrequencyTable _frequencies;
    private readonly ISessionLog? _log;
    private readonly ContactEngine _engine;
    private readonly List<RawDecodeLine> _raw = [];
    private readonly List<(DateTime Slot, int Decodes)> _slotCounts = [];
    private readonly List<SlotActivity> _activity = [];
    private readonly List<(DateTime Slot, SlotParity Parity, List<int> Offsets)> _recentOffsets = [];
    private readonly Dictionary<string, ServiceStatus> _services = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CallsignCacheEntry> _lookups = new(StringComparer.Ordinal);
    private readonly List<TonightContact> _tonight = [];
    private const int MaxNeighbourReports = 50000;
    private IReadOnlyDictionary<string, BandOpening> _openings = new Dictionary<string, BandOpening>(StringComparer.Ordinal);
    private DateTime _openingsUtc;
    private BandActivityFeed _bandFeed;
    private bool _openingsDirty;
    private StationTracker _tracker;
    private PileupTracker _pileups;
    private readonly List<NeighbourReport> _neighbours = [];
    private SessionConfig _config;
    private LogIndex _logIndex = LogIndex.Empty;
    private HearsMe _hearsMe;
    private LotwUsers? _lotwUsers;
    private readonly ReceptionHistory _receptionHistory = new();
    private EntityMatch? _myEntity;
    private long _dialHz;
    private int _txOffset = 1500;
    private DateTime? _rigTuningUntil;
    private bool _txOffsetClear = true;
    private DateTime? _lastDecodedSlot;
    private TimeSpan _lastDecodeTime;
    private double? _medianDt;
    private double? _sntpOffset;
    private string? _fault;
    private TxMeterResult? _txMeters;
    private Task _decodeTask = Task.CompletedTask;
    private DateTime? _recordingSlot;
    private DateTime? _quickSlot;
    private readonly Dictionary<DateTime, HashSet<string>> _fedToEngine = [];
    private DateTime? _finishedShownAt;
    private DateTime _lastContactUtc = DateTime.MinValue;
    private SessionSnapshot _snapshot = new();
    private int? _watchdogCap;

    /// <summary>Creates a session.</summary>
    public Session(SessionConfig config, IDecoder decoder, ISlotSource source, Transmitter tx, IClock clock, CountryFile countries,
                   FrequencyTable frequencies, ISessionLog? log = null)
    {
        _config = config;
        _decoder = decoder;
        _source = source;
        _tx = tx;
        _clock = clock;
        _countries = countries;
        _frequencies = frequencies;
        _log = log;
        _tracker = new StationTracker(config.MyCall, config.MyGrid, countries, config.Mode);
        _pileups = new PileupTracker(config.Mode);
        _hearsMe = new HearsMe(config.Band, available: false);
        _myEntity = countries.Lookup(config.MyCall);
        _dialHz = frequencies.Find(config.Band, config.Mode)?.DialHz ?? 14_074_000;
        _txOffset = config.FixedTxOffsetHz ?? 1500;
        _engine = new ContactEngine(config.MyCall, config.MyGrid, config.Contact, clock.UtcNow);
        _engine.Logged += OnLogged;
        _tx.Started += OnTxStarted;
        _tx.Ended += (_, _) => Publish();
        _tx.Fault += OnTxFault;
        foreach (var name in new[] { ServiceNames.Radio, ServiceNames.Audio, ServiceNames.Decoder, ServiceNames.Qrz, ServiceNames.Psk })
            _services[name] = ServiceStatus.Off("Not started");
        Publish();
    }

    /// <summary>Raised after every change, on the thread that made it. Subscribers marshal to the UI thread.</summary>
    public event Action<SessionSnapshot>? Changed;

    /// <summary>Raised after each decoded slot with its decodes (for interop and the journal).</summary>
    public event Action<DateTime, Mode, IReadOnlyList<Decode>>? SlotDecoded;

    /// <summary>Raised after each decoded slot with its audio and decode count (for WAV saving).</summary>
    public event Action<SlotAudio, int>? SlotAudioDecoded;

    /// <summary>Raised when a transmission starts (for the journal and interop).</summary>
    public event Action<PreparedTx, DateTime>? Transmitting;

    /// <summary>Raised when the band or mode changes (for interop Clear).</summary>
    public event Action? BandChanged;

    /// <summary>The latest snapshot.</summary>
    public SessionSnapshot Snapshot
    {
        get { lock (_gate) return _snapshot; }
    }

    /// <summary>Completes when the decode in flight (if any) has been applied.</summary>
    public Task DecodeIdle
    {
        get { lock (_gate) return _decodeTask; }
    }

    /// <summary>Decoder runs started so far (full and quick), so tests can wait for each to reach the decoder.</summary>
    internal int DecodesStarted { get; private set; }

    /// <summary>The configuration.</summary>
    public SessionConfig Config
    {
        get { lock (_gate) return _config; }
    }

    /// <summary>The country file.</summary>
    public CountryFile Countries => _countries;

    /// <summary>The log index in use.</summary>
    public LogIndex LogIndex
    {
        get { lock (_gate) return _logIndex; }
    }

    // ---------------------------------------------------------------- slot clock

    /// <summary>Slot clock events, on the slot thread. Never waits on I/O.</summary>
    public void OnSlotEvent(SlotEvent e)
    {
        switch (e.Mark)
        {
            case SlotMark.Start:
                OnSlotStart(e);
                break;
            case SlotMark.CaptureCutoff:
                OnCutoff(e);
                break;
        }
        _tx.OnSlotEvent(e);
    }

    private void OnSlotStart(SlotEvent e)
    {
        lock (_gate)
        {
            if (_finishedShownAt is { } f && e.SlotStartUtc > f)
            {
                _engine.ClearFinished();
                _finishedShownAt = null;
            }
            else if (_finishedShownAt is null && _engine.View is { Outcome: not ContactOutcome.InProgress })
            {
                _finishedShownAt = e.SlotStartUtc; // an ended contact stays in the top slot for one slot
            }
        }
        PlanFor(e.SlotStartUtc, e.Mode, late: false);
        var transmitting = _tx.Pending is { } p && p.Plan.SlotStartUtc == e.SlotStartUtc;
        lock (_gate) _recordingSlot = transmitting ? null : e.SlotStartUtc;
        if (transmitting) _source.CancelSlot();
        else _source.BeginSlot(e.SlotStartUtc, e.Mode);
        Publish();
    }

    private void OnCutoff(SlotEvent e)
    {
        lock (_gate)
        {
            if (_recordingSlot != e.SlotStartUtc) return;
            _recordingSlot = null;
        }
        var audio = _source.Cutoff(e.SlotStartUtc, e.Mode);
        if (audio is null) return;
        lock (_gate)
        {
            DecodesStarted++;
            var full = Task.Run(() => DecodeAsync(audio, CancellationToken.None));
            if (QuickContext() is { } quick)
            {
                DecodesStarted++;
                _quickSlot = e.SlotStartUtc;
                _decodeTask = Task.WhenAll(full, Task.Run(() => QuickDecodeAsync(audio, quick)));
            }
            else
            {
                _decodeTask = full;
            }
        }
    }

    /// <summary>
    /// A quick extra decode whose results reach the contact engine long before the full depth-3 decode finishes (which
    /// takes 3 s or more on a busy band), so my next message is chosen on time. In a contact: only the other station's
    /// frequency, full depth and a-priori. Calling CQ: the whole band at depth 1. Otherwise none.
    /// </summary>
    private DecodeContext? QuickContext()
    {
        var myGrid = Grid.Grid4(_config.MyGrid) ?? string.Empty;
        if (_engine.InContact && _engine.DxOffsetHz is { } dx)
        {
            return new DecodeContext(_config.MyCall, myGrid, _engine.DxCall, _engine.DxGrid, LowHz: Math.Max(100, dx - QuickSpanHz),
                HighHz: dx + QuickSpanHz, Depth: 3, QsoProgress: _engine.QsoProgress, RxOffsetHz: dx);
        }
        if (_engine.CallingCq) return new DecodeContext(_config.MyCall, myGrid, null, null, Depth: 1, RxOffsetHz: _txOffset);
        return null;
    }

    private async Task QuickDecodeAsync(SlotAudio audio, DecodeContext ctx)
    {
        DecodeResult result;
        try
        {
            result = await _decoder.DecodeAsync(audio, ctx, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new DecodeResult([], TimeSpan.Zero, DecodeStatus.Failed, ex.Message);
        }
        lock (_gate)
        {
            if (_quickSlot == audio.SlotStartUtc) _quickSlot = null;
            if (audio.Mode != _config.Mode) return;
            FeedEngine(audio.SlotStartUtc, audio.Mode, result.Decodes.Select(HeardMessage.From).ToList(), _clock.UtcNow);
        }
        PlanFor(SlotMath.SlotStart(_clock.UtcNow, audio.Mode), audio.Mode, late: true);
        Publish();
    }

    /// <summary>
    /// Passes a slot's decodes to the contact engine, each message once: the quick and the full decode of a slot both
    /// find the other station's message, and a second copy would read as the other station repeating itself.
    /// </summary>
    private void FeedEngine(DateTime slot, Mode mode, List<HeardMessage> heard, DateTime now)
    {
        // Decodes belonging to an earlier slot never trigger a transmission.
        if (slot + ModeInfo.SlotLength(mode) < SlotMath.SlotStart(now, mode)) return;
        if (!_fedToEngine.TryGetValue(slot, out var fed)) _fedToEngine[slot] = fed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var old in _fedToEngine.Keys.Where(k => k < slot - TimeSpan.FromMinutes(1)).ToList()) _fedToEngine.Remove(old);
        var fresh = heard.Where(h => h.Decode.LowConfidence || !fed.Contains(h.Decode.Text)).ToList();
        foreach (var h in fresh.Where(h => !h.Decode.LowConfidence)) fed.Add(h.Decode.Text);
        _engine.OnDecodes(slot, mode, fresh, now);
    }

    /// <summary>Decodes one slot and applies it. Public so tests and simulation can drive slots directly.</summary>
    public async Task DecodeAsync(SlotAudio audio, CancellationToken ct)
    {
        DecodeContext ctx;
        lock (_gate)
        {
            ctx = new DecodeContext(_config.MyCall, Grid.Grid4(_config.MyGrid) ?? string.Empty, _engine.DxCall, _engine.DxGrid,
                QsoProgress: _engine.QsoProgress, RxOffsetHz: _engine.DxOffsetHz ?? _txOffset);
        }
        DecodeResult result;
        try
        {
            result = await _decoder.DecodeAsync(audio, ctx, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new DecodeResult([], TimeSpan.Zero, DecodeStatus.Failed, ex.Message);
        }
        ApplyDecodes(audio.SlotStartUtc, audio.Mode, result);
        SlotAudioDecoded?.Invoke(audio, result.Decodes.Count);
    }

    private void ApplyDecodes(DateTime slot, Mode mode, DecodeResult result)
    {
        lock (_gate)
        {
            if (_quickSlot == slot) _quickSlot = null;
            _services[ServiceNames.Decoder] = result.Succeeded
                ? new ServiceStatus(ServiceHealth.Ok, $"{result.Decodes.Count} decodes in {result.Elapsed.TotalSeconds:0.0} s", _clock.UtcNow)
                : new ServiceStatus(ServiceHealth.Degraded, result.Message, _clock.UtcNow);
            if (mode != _config.Mode) return;
            var now = _clock.UtcNow;
            var heard = result.Decodes.Select(HeardMessage.From).ToList();
            foreach (var h in heard)
            {
                var sender = h.Message.SenderCall;
                _raw.Add(new RawDecodeLine(h.Decode, h.Message, sender is null ? null : _countries.Lookup(sender)?.Entity.Name,
                    h.Message.IsAddressedTo(_config.MyCall)));
            }
            if (_raw.Count > MaxRaw) _raw.RemoveRange(0, _raw.Count - MaxRaw);

            _tracker.KeepFor = _config.Ranking.KeepCqCallers;
            _tracker.ApplySlot(slot, heard);
            _pileups.ApplySlot(slot, heard);
            FeedEngine(slot, mode, heard, now);

            _slotCounts.Add((slot, result.Decodes.Count));
            _slotCounts.RemoveAll(x => now - x.Slot > TimeSpan.FromHours(1));
            var modeKey = ModeInfo.Name(_config.Mode);
            var needed = _tracker.Stations.Where(st => st.LastHeardUtc == slot)
                .Count(st => _logIndex.Need(st.Call, st.EntityKey, st.Grid, _config.Band, modeKey, _config.Ranking.TierOrder, _config.Ranking.ConfirmedOnly).Tag != NeedTag.Worked);
            _activity.Add(new SlotActivity(slot, result.Decodes.Count, needed));
            _activity.RemoveAll(x => now - x.SlotStartUtc > TimeSpan.FromHours(1));
            _lastDecodedSlot = slot;
            _lastDecodeTime = result.Elapsed;
            _medianDt = ClockEstimate.MedianDt(result.Decodes.Select(d => d.Dt)) ?? _medianDt;

            var parity = SlotMath.Parity(slot, mode);
            _recentOffsets.Add((slot, parity, result.Decodes.Select(d => d.OffsetHz).ToList()));
            if (_recentOffsets.Count > 8) _recentOffsets.RemoveAt(0);
            UpdateTxOffset(mode);

            _hearsMe.Prune(now);
        }
        SlotDecoded?.Invoke(slot, mode, result.Decodes);
        // Decodes that arrive after the next slot has begun may still change (or start) its transmission
        // while its audio has not started, up to the late-start limit.
        PlanFor(SlotMath.SlotStart(_clock.UtcNow, mode), mode, late: true);
        Publish();
    }

    private void UpdateTxOffset(Mode mode)
    {
        if (_config.FixedTxOffsetHz is { } fixedHz)
        {
            _txOffset = fixedHz;
            _txOffsetClear = true;
            return;
        }
        if (_engine.InContact) return; // never change during a contact
        var myParity = _engine.CallingCq ? _engine.CqParity : _config.CqParity;
        var slots = _recentOffsets.Where(x => x.Parity == myParity).Reverse().Take(4).Select(x => (IReadOnlyList<int>)x.Offsets).ToList();
        var c = TxOffsetPicker.Choose(slots, mode, _txOffset);
        _txOffset = c.OffsetHz;
        _txOffsetClear = c.Clear;
    }

    // ---------------------------------------------------------------- transmit planning

    private void PlanFor(DateTime slot, Mode mode, bool late, bool command = false)
    {
        TxPlan? plan;
        TxGuardInput guard;
        int offset;
        PreparedTx? pending;
        bool held;
        TxHold? hold = null;
        var stopTune = false;
        var txStart = slot + TimeSpan.FromMilliseconds(ModeInfo.TxStartMilliseconds(mode));
        var pttDue = txStart - _config.PttLead;
        lock (_gate)
        {
            var now = _clock.UtcNow;
            // An operator command (calling a station, CQ) may still start in the current slot well after its Tx start,
            // leaving out the part already past; anything automatic only within the late-start limit.
            if (late && now > txStart + (command ? _config.CommandStartLimit : _config.LateStartLimit)) return;
            if (_tx.AudioStarted(slot)) return; // already on the air with this slot's message
            pending = _tx.Pending is { } p && p.Plan.SlotStartUtc == slot ? p : null;
            held = pending is not null && _tx.Held;
            plan = _engine.PlanTransmission(slot, mode, now);
            if (plan is null)
            {
                if (_engine.StopReason is { } why) _fault = why;
                if (pending is not null) _tx.CancelPending();
                return;
            }
            stopTune = _tx.CurrentMessage == TuneMessage;
            if (pending is not null && pending.Plan.Message == plan.Message)
            {
                // The decodes did not change the message: send it now instead of waiting out the hold.
                if (held && _tx.Release() && now > pttDue) _tx.BeginLate(pending, _config.PttLead);
                return;
            }
            // The quick decode of the previous slot (the other station's) has not finished and may change this message:
            // hold the transmission until it is applied, up to the late-start limit (audio by Tx start + limit). The
            // full decode is not waited for: it takes longer than the limit on a busy band.
            if (!late && _quickSlot == slot - ModeInfo.SlotLength(mode) && (_engine.InContact || _engine.CallingCq))
                hold = new TxHold(txStart + _config.LateStartLimit - _config.PttLead, _config.PttLead);
            offset = _txOffset;
            guard = new TxGuardInput(_config.Band, _dialHz, _sntpOffset ?? _medianDt, _config.ClockBlockSeconds,
                _services[ServiceNames.Radio].Health == ServiceHealth.Down ? _services[ServiceNames.Radio].Message : null, true);
        }
        // A Tune tone still on the air gives way to the message the operator just asked for.
        if (stopTune) _ = _tx.HaltAsync();

        var prepared = _tx.Prepare(plan, mode, offset, guard, out var refusal, hold);
        if (prepared is null)
        {
            lock (_gate)
            {
                _fault = refusal;
                _engine.Abandon(_clock.UtcNow);
            }
            Publish();
            return;
        }
        lock (_gate)
        {
            _fault = null;
            // Nothing is recorded or decoded in a slot we transmit in.
            if (_recordingSlot == slot)
            {
                _recordingSlot = null;
                _source.CancelSlot();
            }
        }
        // Its marks have passed: nothing was keyed for it yet (no earlier message, or the earlier one was held).
        if ((pending is null || held) && _clock.UtcNow > pttDue)
        {
            // Up to the late-start limit the whole message goes out, shifted later; beyond it (a command only) it keeps
            // its place in the slot and the part already past is left out.
            var align = command && _clock.UtcNow + _config.PttLead > txStart + _config.LateStartLimit ? txStart : (DateTime?)null;
            _tx.BeginLate(prepared, _config.PttLead, align);
        }
    }

    private void OnTxStarted(PreparedTx tx, DateTime startedUtc)
    {
        lock (_gate)
        {
            _engine.OnTransmitted(tx.Plan, startedUtc);
            _raw.Add(new RawDecodeLine(new Decode(tx.Plan.SlotStartUtc, 0, 0, tx.OffsetHz, tx.Plan.Message, false), MessageParser.Parse(tx.Plan.Message),
                null, false, Transmitted: true));
            if (_engine.View is { Outcome: not ContactOutcome.InProgress }) _finishedShownAt = tx.Plan.SlotStartUtc;
        }
        Transmitting?.Invoke(tx, startedUtc);
        Publish();
    }

    private void OnTxFault(string message)
    {
        lock (_gate)
        {
            _fault = message;
            _engine.Abandon(_clock.UtcNow);
        }
        Publish();
    }

    private void OnLogged(ContactLogEntry entry)
    {
        LogContext ctx;
        lock (_gate)
        {
            var s = _tracker.Find(entry.Call);
            var lk = _lookups.GetValueOrDefault(Callsign.Normalize(entry.Call));
            // A caller can skip the grid exchange. Persist the same known grid that we index.
            entry = entry with { Grid = entry.Grid ?? s?.Grid ?? lk?.Grid };
            var entity = s?.Entity ?? _countries.Lookup(entry.Call);
            var need = _logIndex.Need(entry.Call, entity?.Entity.Key, entry.Grid, _config.Band, ModeInfo.Name(_config.Mode),
                _config.Ranking.TierOrder, _config.Ranking.ConfirmedOnly);
            ctx = new LogContext(_config.Band, _config.Mode, _dialHz + _txOffset, _config.MyCall, _config.MyGrid, _config.PowerWatts, need,
                entity?.Entity.Key, entity?.Entity.Name, lk?.Name, lk?.State, entity?.Continent, lk?.City);
            if (entry.TimeOffUtc - _lastContactUtc > TimeSpan.FromHours(2)) _tonight.Clear();
            _lastContactUtc = entry.TimeOffUtc;
            _tonight.Add(new TonightContact(entry.Call, _config.Band, entry.TimeOffUtc, need.Tag));
            _logIndex.Add(new LogContact(entry.Call, _config.Band, ModeInfo.Name(_config.Mode), entry.Grid, entity?.Entity.Key, lk?.State,
                false, entry.TimeOnUtc));
            _finishedShownAt ??= SlotMath.SlotStart(_clock.UtcNow, _config.Mode);
        }
        _log?.Write(entry, ctx);
    }

    // ---------------------------------------------------------------- commands

    /// <summary>Operator input (key or click), for the Tx watchdog.</summary>
    public void Activity()
    {
        lock (_gate) _engine.OperatorActivity(_clock.UtcNow);
    }

    /// <summary>Calls a station from the line or the raw decodes.</summary>
    public bool CallStation(string call)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            if (_engine.Waiting.Any(w => Callsign.EqualsCall(w.Call, call)))
            {
                _engine.AnswerCaller(call, now);
            }
            else
            {
                var s = _tracker.Find(call);
                if (s is null) return false;
                // A station heard working someone else (reports or grids exchanged, not signing off) will not hear me:
                // the contact ends at once and says so rather than calling into its contact.
                var busyWith = s is { State: StationState.InContact, Partner: { } p } && !Callsign.EqualsCall(p, _config.MyCall) ? p : null;
                _engine.CallStation(s.Call, s.Grid, s.LastSnr, s.Parity, s.OffsetHz, now, busyWith);
            }
            _fault = null;
        }
        AfterCommand();
        return true;
    }

    /// <summary>Starts or stops calling CQ.</summary>
    public void ToggleCq()
    {
        lock (_gate)
        {
            if (_engine.CallingCq) _engine.StopCq(_clock.UtcNow);
            else _engine.CallCq(_config.CqParity, _clock.UtcNow);
            _fault = null;
        }
        AfterCommand();
    }

    /// <summary>Answers a waiting caller now.</summary>
    public void AnswerCaller(string call)
    {
        lock (_gate) _engine.AnswerCaller(call, _clock.UtcNow);
        AfterCommand();
    }

    /// <summary>Answers a waiting caller when the current contact ends.</summary>
    public void AnswerAfterThisContact(string call)
    {
        lock (_gate) _engine.AnswerAfterThisContact(call, _clock.UtcNow);
        Publish();
    }

    /// <summary>Resend the current step.</summary>
    public void Resend()
    {
        lock (_gate) _engine.Resend(_clock.UtcNow);
        AfterCommand();
    }

    /// <summary>Log the contact now.</summary>
    public void LogNow()
    {
        lock (_gate) _engine.LogNow(_clock.UtcNow);
        Publish();
    }

    /// <summary>Jump to one of my upcoming steps.</summary>
    public void JumpTo(int step)
    {
        lock (_gate) _engine.JumpTo(step, _clock.UtcNow);
        _tx.CancelPending();
        AfterCommand();
    }

    /// <summary>Abandon the contact: stop transmitting, log nothing.</summary>
    public async Task AbandonAsync()
    {
        lock (_gate) _engine.Abandon(_clock.UtcNow);
        await _tx.HaltAsync().ConfigureAwait(false);
        Publish();
    }

    /// <summary>Halt Tx: drop PTT now and stop sequencing.</summary>
    public Task HaltAsync() => HaltAsync(null);

    /// <summary>Halt Tx with a reason for the bottom bar (for a stop the operator did not ask for).</summary>
    public async Task HaltAsync(string? reason)
    {
        lock (_gate)
        {
            _engine.Halt(_clock.UtcNow);
            _rigTuningUntil = null;
            if (reason is not null) _fault = reason;
        }
        await _tx.HaltAsync().ConfigureAwait(false);
        Publish();
    }

    /// <summary>
    /// Caps the Tx watchdog (minutes) while a remote device has control, or removes the cap with null. The operator's
    /// setting is kept and applies again once the cap is removed.
    /// </summary>
    public void SetWatchdogCap(int? minutes)
    {
        lock (_gate)
        {
            if (_watchdogCap == minutes) return;
            _watchdogCap = minutes;
            _engine.Configure(_config.MyCall, _config.MyGrid, CappedContact());
        }
    }

    /// <summary>Message text of a Tune tone while it is on the air.</summary>
    public const string TuneMessage = "Tune";

    /// <summary>
    /// Tune, from an operator's click (owner request 2026-10-12): starts the radio's own antenna tuner when it has one
    /// and accepts the command, otherwise a steady tone at the Tx offset for up to <paramref name="seconds"/>, with PTT
    /// on now. Refused while CQ, a contact or another transmission is under way, and by the same radio, audio and band
    /// guards as any transmission. Returns null once the tuner or tone is going, otherwise why not.
    /// </summary>
    public async Task<string?> TuneAsync(double seconds)
    {
        TxGuardInput guard;
        int offset;
        lock (_gate)
        {
            if (_engine.CallingCq || _engine.InContact || _tx.Transmitting || _rigTuningUntil > _clock.UtcNow)
                return "Tune is only available while nothing else is being sent.";
            offset = _txOffset;
            guard = new TxGuardInput(_config.Band, _dialHz, _sntpOffset ?? _medianDt, _config.ClockBlockSeconds,
                _services[ServiceNames.Radio].Health == ServiceHealth.Down ? _services[ServiceNames.Radio].Message : null, true);
        }
        if (await _tx.StartRigTunerAsync(offset, guard).ConfigureAwait(false))
        {
            lock (_gate)
            {
                _rigTuningUntil = _clock.UtcNow + TimeSpan.FromSeconds(seconds);
                _fault = null;
            }
            Publish();
            return null;
        }
        var why = await _tx.TuneAsync(offset, seconds, guard).ConfigureAwait(false);
        lock (_gate) _fault = why is null ? null : "No tune tone was sent. " + why;
        Publish();
        return why;
    }

    /// <summary>True when something is (or is about to be) transmitted: CQ running, a contact in progress, or on the air.</summary>
    public bool WantsTransmit
    {
        get
        {
            lock (_gate) return _engine.CallingCq || _engine.InContact || _tx.Transmitting;
        }
    }

    private ContactSettings CappedContact() => _watchdogCap is { } cap && cap < _config.Contact.WatchdogMinutes
        ? _config.Contact with { WatchdogMinutes = cap }
        : _config.Contact;

    /// <summary>Sets a fixed transmit offset, or automatic with null.</summary>
    public void SetTxOffset(int? hz)
    {
        lock (_gate)
        {
            _config = _config with { FixedTxOffsetHz = hz };
            if (hz is { } v) _txOffset = v;
            else UpdateTxOffset(_config.Mode);
        }
        Publish();
    }

    /// <summary>Changes band (and dial frequency): clears the station list, which belongs to the old band.</summary>
    public void SetBand(string band, Mode mode)
    {
        lock (_gate)
        {
            var oldMode = _config.Mode;
            _config = _config with { Band = band, Mode = mode };
            _dialHz = _frequencies.Find(band, mode)?.DialHz ?? _dialHz;
            _tracker = new StationTracker(_config.MyCall, _config.MyGrid, _countries, mode);
            _pileups = new PileupTracker(mode);
            foreach (var (call, e) in _lookups) _tracker.SetLookup(call, e.Grid, e.State, e.City);
            _engine.Abandon(_clock.UtcNow);
            _engine.StopCq(_clock.UtcNow);
            _hearsMe = new HearsMe(band, _hearsMe.Available);
            // Reports of nearby stations cover every band (for the band menu); only a mode change makes them irrelevant.
            if (mode != oldMode)
            {
                _neighbours.Clear();
                _openingsUtc = default;
            }
            _recentOffsets.Clear();
            _slotCounts.Clear();
            _activity.Clear();
            _txMeters = null; // SWR belongs to the antenna on the old band
        }
        BandChanged?.Invoke();
        Publish();
    }

    /// <summary>Records the dial frequency read from the radio.</summary>
    public void SetDial(long hz)
    {
        lock (_gate) _dialHz = hz;
        Publish();
    }

    /// <summary>Applies new settings (call, grid, ranking, contact).</summary>
    public void Reconfigure(SessionConfig config)
    {
        lock (_gate)
        {
            var stationChanged = config.MyCall != _config.MyCall || config.MyGrid != _config.MyGrid;
            _config = config;
            _engine.Configure(config.MyCall, config.MyGrid, CappedContact());
            if (stationChanged)
            {
                _receptionHistory.Clear();
                _tracker.SetMyStation(config.MyCall, config.MyGrid);
                _myEntity = _countries.Lookup(config.MyCall);
            }
        }
        Publish();
    }

    /// <summary>Sets who uploads to LoTW (for ranking when only confirmed contacts count).</summary>
    public void SetLotwUsers(LotwUsers users)
    {
        lock (_gate) _lotwUsers = users;
        Publish();
    }

    /// <summary>Swaps in a rebuilt log index (after sync or import).</summary>
    public void SetLogIndex(LogIndex index)
    {
        lock (_gate) _logIndex = index;
        Publish();
    }

    /// <summary>Sets a service status.</summary>
    public void SetService(string name, ServiceStatus status)
    {
        lock (_gate) _services[name] = status;
        Publish();
    }

    /// <summary>Records the ALC and SWR meters from the latest transmission.</summary>
    public void SetTxMeters(TxMeterResult result)
    {
        lock (_gate) _txMeters = result;
        Publish();
    }

    /// <summary>Records the SNTP clock offset.</summary>
    public void SetClockOffset(double? seconds)
    {
        lock (_gate) _sntpOffset = seconds;
        Publish();
    }

    /// <summary>PSK Reporter availability.</summary>
    public void SetReportsAvailable(bool available)
    {
        lock (_gate) _hearsMe.Available = available;
        Publish();
    }

    /// <summary>Adds a reception report of my signal.</summary>
    public void AddReport(ReceptionReport r)
    {
        lock (_gate)
        {
            _hearsMe.Add(r);
            _receptionHistory.Add(r, _clock.UtcNow);
        }
        Publish();
    }

    /// <summary>Sets which bands the reports of stations near me are being followed on.</summary>
    public void SetBandActivityFeed(BandActivityFeed feed)
    {
        lock (_gate)
        {
            if (_bandFeed == feed) return;
            _bandFeed = feed;
        }
        Publish();
    }

    /// <summary>
    /// Adds a report of another station, kept only when it is near me in this mode (on any band: the band menu shows
    /// them all). Shown at the next publish (they can arrive several a second, so they do not publish on their own).
    /// </summary>
    public void AddNeighbourReport(NeighbourReport r)
    {
        lock (_gate)
        {
            if (r.Mode != ModeInfo.Name(_config.Mode) || Callsign.EqualsCall(r.SenderCall, _config.MyCall)) return;
            if (Grid.ToLatLon(_config.MyGrid) is not { } me || !NeighbourComparison.IsNeighbour(me, r.SenderGrid)) return;
            _neighbours.Add(r);
            _openingsDirty = true;
            var since = _clock.UtcNow - NeighbourComparison.Window;
            if (_neighbours.Count > MaxNeighbourReports || _neighbours[0].TimeUtc < since) _neighbours.RemoveAll(x => x.TimeUtc < since);
            if (_neighbours.Count > MaxNeighbourReports) _neighbours.RemoveRange(0, _neighbours.Count - MaxNeighbourReports);
        }
    }

    // The band menu's activity. It looks up the country of every receiver, so while reports stream in it is rebuilt at
    // most once a slot (sooner when it is empty). Under _gate.
    private IReadOnlyDictionary<string, BandOpening> Openings(DateTime now)
    {
        var stale = now - _openingsUtc >= ModeInfo.SlotLength(_config.Mode);
        if (!stale && !(_openingsDirty && _openings.Count == 0)) return _openings;
        _openingsUtc = now;
        _openingsDirty = false;
        _openings = BandOpenings.Build(_neighbours, _config.MyCall, _config.MyGrid, ModeInfo.Name(_config.Mode), _countries, _logIndex, now);
        return _openings;
    }

    /// <summary>Adds QRZ lookup results (fresh or from the cache) and publishes once.</summary>
    public void SetLookups(IEnumerable<CallsignCacheEntry> entries)
    {
        lock (_gate)
        {
            foreach (var e in entries)
            {
                var call = Callsign.Normalize(e.Call);
                _lookups[call] = e;
                _tracker.SetLookup(call, e.Grid, e.State, e.City);
            }
        }
        Publish();
    }

    /// <summary>QRZ name for a call, if looked up.</summary>
    public string? NameOf(string call) => LookupOf(call)?.Name;

    /// <summary>QRZ data for a call, if looked up.</summary>
    public CallsignCacheEntry? LookupOf(string call)
    {
        lock (_gate) return _lookups.GetValueOrDefault(Callsign.Normalize(call));
    }

    /// <summary>True when QRZ data for the call is already held (so the cache need not be read again).</summary>
    public bool HasLookup(string call)
    {
        lock (_gate) return _lookups.ContainsKey(Callsign.Normalize(call));
    }

    private void AfterCommand()
    {
        // A command may start a transmission in the current slot if its start is still within the late limit.
        var mode = Config.Mode;
        PlanFor(SlotMath.SlotStart(_clock.UtcNow, mode), mode, late: true, command: true);
        Publish();
    }

    // ---------------------------------------------------------------- snapshots

    /// <summary>Recomputes and publishes the snapshot.</summary>
    public void Publish()
    {
        SessionSnapshot snap;
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var stations = _tracker.Stations.ToList();
            var rank = Ranker.Rank(new RankInput(stations, _logIndex, _hearsMe.Available ? _hearsMe : null, _config.Ranking, _myEntity,
                _config.Band, ModeInfo.Name(_config.Mode), now, _countries, _lotwUsers));
            var summary = BandSummaryCalculator.Compute(stations, _logIndex, _config.Band, ModeInfo.Name(_config.Mode), now, _slotCounts, _tonight,
                _config.Ranking);
            snap = new SessionSnapshot
            {
                MyCall = _config.MyCall,
                MyGrid = _config.MyGrid,
                Band = _config.Band,
                Mode = _config.Mode,
                DialHz = _dialHz,
                Rank = rank,
                Stations = stations,
                Summary = summary,
                RawDecodes = _raw.ToList(),
                LastDecodedSlot = _lastDecodedSlot,
                LastDecodeTime = _lastDecodeTime,
                MedianDt = _medianDt,
                LogIsEmpty = _logIndex.IsEmpty,
                Tonight = _tonight.ToList(),
                Reports = _hearsMe.Reports.OrderByDescending(r => r.TimeUtc).ToList(),
                ReceptionHistory = _receptionHistory.Buckets(_config.Band, ModeInfo.Name(_config.Mode), now),
                Neighbours = NeighbourComparison.Compare(_hearsMe.Reports, _neighbours, _config.MyCall, _config.MyGrid, _config.Band,
                    ModeInfo.Name(_config.Mode), _countries, now),
                BandOpenings = Openings(now),
                BandActivityFeed = _bandFeed,
                Services = new Dictionary<string, ServiceStatus>(_services),
                Contact = _engine.View,
                Transmitting = _tx.Transmitting,
                TransmittingMessage = _tx.CurrentMessage,
                CallingCq = _engine.CallingCq,
                RigTuningUntilUtc = _rigTuningUntil is { } until && until > now ? until : null,
                TxOffsetHz = _txOffset,
                TxOffsetClear = _txOffsetClear,
                TxOffsetAuto = _config.FixedTxOffsetHz is null,
                Fault = _fault ?? _engine.StopReason,
                ClockOffsetSeconds = _sntpOffset ?? _medianDt,
                Waiting = _engine.Waiting.ToList(),
                TxMeters = _txMeters,
                Activity = _activity.ToList(),
                Pileups = _pileups.All(now),
            };
            _snapshot = snap;
        }
        Changed?.Invoke(snap);
    }
}
