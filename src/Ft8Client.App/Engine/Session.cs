// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
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
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;

namespace Ft8Client.App.Engine;

/// <summary>
/// The orchestrator: slot clock events in, decodes through the tracker, contact engine and ranker, transmissions
/// out through the transmitter, immutable snapshots to the UI. Headless; the UI only renders snapshots and sends
/// commands. State changes happen under one lock; decoding and I/O happen outside it.
/// </summary>
public sealed class Session
{
    private const int MaxRaw = 1500;
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
    private readonly List<(DateTime Slot, SlotParity Parity, List<int> Offsets)> _recentOffsets = [];
    private readonly Dictionary<string, ServiceStatus> _services = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string? Name, string? State)> _lookups = new(StringComparer.Ordinal);
    private readonly List<TonightContact> _tonight = [];
    private StationTracker _tracker;
    private SessionConfig _config;
    private LogIndex _logIndex = LogIndex.Empty;
    private HearsMe _hearsMe;
    private EntityMatch? _myEntity;
    private long _dialHz;
    private int _txOffset = 1500;
    private bool _txOffsetClear = true;
    private DateTime? _lastDecodedSlot;
    private TimeSpan _lastDecodeTime;
    private double? _medianDt;
    private double? _sntpOffset;
    private string? _fault;
    private Task _decodeTask = Task.CompletedTask;
    private DateTime? _recordingSlot;
    private DateTime? _finishedShownAt;
    private DateTime _lastContactUtc = DateTime.MinValue;
    private SessionSnapshot _snapshot = new();

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
        lock (_gate) _decodeTask = Task.Run(() => DecodeAsync(audio, CancellationToken.None));
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
    }

    private void ApplyDecodes(DateTime slot, Mode mode, DecodeResult result)
    {
        lock (_gate)
        {
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

            _tracker.ApplySlot(slot, heard);
            // Decodes belonging to an earlier slot never trigger a transmission.
            var current = SlotMath.SlotStart(now, mode);
            var stale = slot + ModeInfo.SlotLength(mode) < current;
            if (!stale) _engine.OnDecodes(slot, mode, heard, now);

            _slotCounts.Add((slot, result.Decodes.Count));
            _slotCounts.RemoveAll(x => now - x.Slot > TimeSpan.FromHours(1));
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

    private void PlanFor(DateTime slot, Mode mode, bool late)
    {
        TxPlan? plan;
        TxGuardInput guard;
        int offset;
        PreparedTx? pending;
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var txStart = slot + TimeSpan.FromMilliseconds(ModeInfo.TxStartMilliseconds(mode));
            if (late && now > txStart + _config.LateStartLimit) return;
            if (_tx.AudioStarted(slot)) return; // already on the air with this slot's message
            pending = _tx.Pending is { } p && p.Plan.SlotStartUtc == slot ? p : null;
            plan = _engine.PlanTransmission(slot, mode, now);
            if (plan is null)
            {
                if (_engine.StopReason is { } why) _fault = why;
                if (pending is not null) _tx.CancelPending();
                return;
            }
            if (pending is not null && pending.Plan.Message == plan.Message) return;
            offset = _txOffset;
            guard = new TxGuardInput(_config.Band, _dialHz, _sntpOffset ?? _medianDt, _config.ClockBlockSeconds,
                _services[ServiceNames.Radio].Health == ServiceHealth.Down ? _services[ServiceNames.Radio].Message : null, true);
        }

        var prepared = _tx.Prepare(plan, mode, offset, guard, out var refusal);
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
        var pttDue = slot + TimeSpan.FromMilliseconds(ModeInfo.TxStartMilliseconds(mode)) - _config.PttLead;
        if (pending is null && _clock.UtcNow > pttDue) _tx.BeginLate(prepared, _config.PttLead);
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
            var entity = s?.Entity ?? _countries.Lookup(entry.Call);
            var need = _logIndex.Need(entry.Call, entity?.Entity.Key, entry.Grid ?? s?.Grid, _config.Band, ModeInfo.Name(_config.Mode),
                _config.Ranking.TierOrder, _config.Ranking.ConfirmedOnly);
            _lookups.TryGetValue(entry.Call, out var lk);
            ctx = new LogContext(_config.Band, _config.Mode, _dialHz + _txOffset, _config.MyCall, _config.MyGrid, _config.PowerWatts, need,
                entity?.Entity.Key, entity?.Entity.Name, lk.Name, lk.State, entity?.Continent);
            if (entry.TimeOffUtc - _lastContactUtc > TimeSpan.FromHours(2)) _tonight.Clear();
            _lastContactUtc = entry.TimeOffUtc;
            _tonight.Add(new TonightContact(entry.Call, _config.Band, entry.TimeOffUtc, need.Tag));
            _logIndex.Add(new LogContact(entry.Call, _config.Band, ModeInfo.Name(_config.Mode), entry.Grid ?? s?.Grid, entity?.Entity.Key, lk.State,
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
                _engine.CallStation(s.Call, s.Grid, s.LastSnr, s.Parity, s.OffsetHz, now);
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
    public async Task HaltAsync()
    {
        lock (_gate) _engine.Halt(_clock.UtcNow);
        await _tx.HaltAsync().ConfigureAwait(false);
        Publish();
    }

    /// <summary>Sets a fixed transmit offset, or automatic with null.</summary>
    public void SetTxOffset(int? hz)
    {
        lock (_gate)
        {
            _config = _config with { FixedTxOffsetHz = hz };
            if (hz is { } v) _txOffset = v;
        }
        Publish();
    }

    /// <summary>Changes band (and dial frequency): clears the station list, which belongs to the old band.</summary>
    public void SetBand(string band, Mode mode)
    {
        lock (_gate)
        {
            _config = _config with { Band = band, Mode = mode };
            _dialHz = _frequencies.Find(band, mode)?.DialHz ?? _dialHz;
            _tracker = new StationTracker(_config.MyCall, _config.MyGrid, _countries, mode);
            _engine.Abandon(_clock.UtcNow);
            _engine.StopCq(_clock.UtcNow);
            _hearsMe = new HearsMe(band, _hearsMe.Available);
            _recentOffsets.Clear();
            _slotCounts.Clear();
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
            _engine.Configure(config.MyCall, config.MyGrid, config.Contact);
            if (stationChanged)
            {
                _tracker.SetMyStation(config.MyCall, config.MyGrid);
                _myEntity = _countries.Lookup(config.MyCall);
            }
        }
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
        lock (_gate) _hearsMe.Add(r);
        Publish();
    }

    /// <summary>Adds a QRZ lookup result for a station.</summary>
    public void SetLookup(string call, string? name, string? grid, string? state)
    {
        lock (_gate)
        {
            _lookups[Callsign.Normalize(call)] = (name, state);
            _tracker.SetLookup(call, grid, state);
        }
        Publish();
    }

    /// <summary>QRZ name for a call, if looked up.</summary>
    public string? NameOf(string call)
    {
        lock (_gate) return _lookups.TryGetValue(Callsign.Normalize(call), out var v) ? v.Name : null;
    }

    private void AfterCommand()
    {
        // A command may start a transmission in the current slot if its start is still within the late limit.
        var mode = Config.Mode;
        PlanFor(SlotMath.SlotStart(_clock.UtcNow, mode), mode, late: true);
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
                _config.Band, ModeInfo.Name(_config.Mode), now, _countries));
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
                Services = new Dictionary<string, ServiceStatus>(_services),
                Contact = _engine.View,
                Transmitting = _tx.Transmitting,
                TransmittingMessage = _tx.CurrentMessage,
                CallingCq = _engine.CallingCq,
                TxOffsetHz = _txOffset,
                TxOffsetClear = _txOffsetClear,
                TxOffsetAuto = _config.FixedTxOffsetHz is null,
                Fault = _fault ?? _engine.StopReason,
                ClockOffsetSeconds = _sntpOffset ?? _medianDt,
                Waiting = _engine.Waiting.ToList(),
            };
            _snapshot = snap;
        }
        Changed?.Invoke(snap);
    }
}
