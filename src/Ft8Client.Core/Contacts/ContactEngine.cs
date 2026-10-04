// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Encoding;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Contacts;

/// <summary>
/// The contact state machine (docs/04-domain-logic.md, section 7). Pure: time comes in with every call and nothing
/// here touches the radio. It never starts a contact by itself: <see cref="CallStation"/>, <see cref="CallCq"/> and
/// <see cref="AnswerCaller"/> are operator commands. Not thread-safe; the session calls it from one worker.
/// </summary>
public sealed class ContactEngine
{
    /// <summary>Window in which a repeated sign-off gets one more reply.</summary>
    public static readonly TimeSpan LateWindow = TimeSpan.FromMinutes(2);

    /// <summary>Maximum stations kept in Waiting for you.</summary>
    public const int MaxWaiting = 5;

    private readonly List<WaitingCaller> _waiting = [];
    private ContactSettings _settings;
    private string _myCall;
    private string _myGrid;
    private Contact? _c;
    private Contact? _finished;
    private bool _cqRunning;
    private SlotParity _cqParity;
    private DateTime _lastInputUtc;
    private Finished? _lastLogged;
    private string? _extraMessage;
    private SlotParity _extraParity;

    /// <summary>Creates the engine.</summary>
    public ContactEngine(string myCall, string myGrid, ContactSettings settings, DateTime nowUtc)
    {
        _myCall = Callsign.Normalize(myCall);
        _myGrid = Grid.Grid4(myGrid) ?? string.Empty;
        _settings = settings;
        _lastInputUtc = nowUtc;
    }

    /// <summary>Raised when a contact should be logged.</summary>
    public event Action<ContactLogEntry>? Logged;

    /// <summary>True while calling CQ.</summary>
    public bool CallingCq => _cqRunning;

    /// <summary>Parity used for CQ.</summary>
    public SlotParity CqParity => _cqParity;

    /// <summary>Why transmitting stopped by itself (the watchdog), until the operator acts again.</summary>
    public string? StopReason { get; private set; }

    /// <summary>Stations that called me, oldest first.</summary>
    public IReadOnlyList<WaitingCaller> Waiting => _waiting;

    /// <summary>True while a contact is in progress.</summary>
    public bool InContact => _c is not null;

    /// <summary>The DX station of the contact in progress.</summary>
    public string? DxCall => _c?.DxCall;

    /// <summary>The DX station's audio offset, for the decoder's receive frequency.</summary>
    public int? DxOffsetHz => _c?.DxOffset;

    /// <summary>The DX station's grid, if known.</summary>
    public string? DxGrid => _c?.DxGrid;

    /// <summary>QSO progress 0 to 5 for jt9 a-priori decoding.</summary>
    public int QsoProgress => _c is null ? 0 : (_c.Flow, _c.Phase) switch
    {
        (ContactFlow.AnswerCq, 0) => 1,
        (ContactFlow.CalledCq, 2) => 2,
        (ContactFlow.CalledCq, _) => 4,
        (_, 2) => 3,
        _ => 5,
    };

    /// <summary>The contact shown in the top slot: the one in progress, or the one that just ended.</summary>
    public ContactView? View => (_c ?? _finished) is { } c ? ViewOf(c) : null;

    /// <summary>Updates my station and settings.</summary>
    public void Configure(string myCall, string myGrid, ContactSettings settings)
    {
        _myCall = Callsign.Normalize(myCall);
        _myGrid = Grid.Grid4(myGrid) ?? string.Empty;
        _settings = settings;
    }

    /// <summary>Records operator input (key or click) for the watchdog.</summary>
    public void OperatorActivity(DateTime nowUtc)
    {
        _lastInputUtc = nowUtc;
        StopReason = null;
    }

    /// <summary>Flow A: call a station (answering its CQ, or replying to its report if it called me first).</summary>
    public void CallStation(string call, string? grid, int snr, SlotParity dxParity, int dxOffsetHz, DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        call = Callsign.Normalize(call);
        _finished = null;
        _c = new Contact(ContactFlow.AnswerCq, call, Grid.Grid4(grid), SlotMath.Opposite(dxParity), Report.Clamp(snr), dxOffsetHz);
        _c.Steps.AddRange(StepsFor(_c));
        RemoveWaiting(call);
    }

    /// <summary>Starts calling CQ in slots of the given parity.</summary>
    public void CallCq(SlotParity parity, DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        _cqRunning = true;
        _cqParity = parity;
    }

    /// <summary>Stops calling CQ. A contact in progress continues.</summary>
    public void StopCq(DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        _cqRunning = false;
    }

    /// <summary>Answers a waiting caller now: report to a grid, R-report to a report.</summary>
    public bool AnswerCaller(string call, DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        var w = _waiting.FirstOrDefault(x => Callsign.EqualsCall(x.Call, call));
        if (w is null) return false;
        _finished = null;
        StartFromCaller(w);
        return true;
    }

    /// <summary>Marks a waiting caller to be answered when the current contact ends.</summary>
    public void AnswerAfterThisContact(string call, DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        var i = _waiting.FindIndex(x => Callsign.EqualsCall(x.Call, call));
        if (i >= 0) _waiting[i] = _waiting[i] with { AnswerAfter = true };
    }

    /// <summary>Sends the current step again in my next slot (or the final 73 again).</summary>
    public void Resend(DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        if (_c is not null)
        {
            _c.AwaitingReply = false;
            if (_c.StepTxCount > 0) _c.StepTxCount--; // an operator resend is not counted against the retry limit
        }
        else if (_finished is { } f && f.Logged)
        {
            _extraMessage = Format(f, 4);
            _extraParity = f.MyParity;
        }
    }

    /// <summary>Logs the contact as it stands and ends it.</summary>
    public void LogNow(DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        if (_c is null)
        {
            if (_finished is { Logged: false } f && f.Outcome == ContactOutcome.Logged) DoLog(f, nowUtc);
            return;
        }
        if (!_c.Logged) DoLog(_c, nowUtc);
        End(ContactOutcome.Logged, nowUtc);
    }

    /// <summary>Ends the contact immediately; logs nothing. Calling CQ stops too.</summary>
    public void Abandon(DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        _cqRunning = false;
        _extraMessage = null;
        if (_c is not null) End(ContactOutcome.Abandoned, nowUtc, resume: false);
    }

    /// <summary>Halt Tx: stops transmitting and sequencing (same as abandon).</summary>
    public void Halt(DateTime nowUtc) => Abandon(nowUtc);

    /// <summary>Manual override: jump the sequence to one of my upcoming steps.</summary>
    public bool JumpTo(int stepIndex, DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        if (_c is null || stepIndex < 0 || stepIndex >= _c.Steps.Count || !_c.Steps[stepIndex].Mine || stepIndex <= _c.Phase) return false;
        if (stepIndex == 0) return false;
        for (var i = _c.Phase; i < stepIndex; i++) _c.Steps[i] = _c.Steps[i] with { State = StepState.Done };
        SetPhase(_c, stepIndex);
        return true;
    }

    /// <summary>
    /// Applies the decodes of a received slot. Only messages from the DX station addressed to my call advance the
    /// contact; other messages to me go to Waiting for you.
    /// </summary>
    public void OnDecodes(DateTime slotStartUtc, Mode mode, IReadOnlyList<HeardMessage> heard, DateTime nowUtc)
    {
        var parity = SlotMath.Parity(slotStartUtc, mode);
        var toMe = new List<(HeardMessage H, ParsedMessage M)>();
        foreach (var h in heard)
        {
            if (h.Decode.LowConfidence) continue;
            foreach (var m in h.Message.Expand())
            {
                if (m.SenderCall is null || !m.IsAddressedTo(_myCall)) continue;
                if (m.To is { } t && !Callsign.EqualsCall(t.Call, _myCall)) continue;
                toMe.Add((h, m));
            }
        }

        if (_c is not null)
        {
            foreach (var (h, m) in toMe.Where(x => Callsign.EqualsCall(x.M.SenderCall, _c.DxCall)))
            {
                Advance(_c, m, slotStartUtc, nowUtc);
                if (_c is null) break;
            }
        }
        else
        {
            LateSignoff(toMe, slotStartUtc);
        }

        // Answer the first caller to my CQ.
        if (_c is null && _cqRunning && parity != _cqParity)
        {
            var first = toMe.FirstOrDefault(x => x.M.Kind is MessageKind.GridReply or MessageKind.Report);
            if (first.M is not null)
            {
                AddWaiting(first.H, first.M, parity, slotStartUtc);
                var w = _waiting.First(x => Callsign.EqualsCall(x.Call, first.M.SenderCall));
                StartFromCaller(w);
            }
        }

        foreach (var (h, m) in toMe)
        {
            if (_c is not null && Callsign.EqualsCall(m.SenderCall, _c.DxCall)) continue;
            if (_lastLogged is { } l && Callsign.EqualsCall(m.SenderCall, l.DxCall) && nowUtc - l.LoggedUtc < LateWindow) continue;
            AddWaiting(h, m, parity, slotStartUtc);
        }
        _waiting.RemoveAll(w => nowUtc - w.LastCalledUtc > TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// What to send in the slot starting at <paramref name="slotStartUtc"/>, or null. Ends the contact as
    /// <see cref="ContactOutcome.NoReply"/> when the retry limit is reached, and stops everything when the watchdog expires.
    /// </summary>
    public TxPlan? PlanTransmission(DateTime slotStartUtc, Mode mode, DateTime nowUtc)
    {
        var parity = SlotMath.Parity(slotStartUtc, mode);
        var wantsTx = _c is not null || _cqRunning || _extraMessage is not null;
        if (!wantsTx) return null;

        if (nowUtc - _lastInputUtc >= TimeSpan.FromMinutes(_settings.WatchdogMinutes))
        {
            _cqRunning = false;
            _extraMessage = null;
            if (_c is not null) End(ContactOutcome.Watchdog, nowUtc, resume: false);
            StopReason = "Stopped by watchdog";
            return null;
        }

        if (_c is not null)
        {
            if (parity != _c.MyParity) return null;
            if (_c.Phase >= _c.Steps.Count || !_c.Steps[_c.Phase].Mine) return null;
            var isFinal = IsFinalCourtesy(_c, _c.Phase);
            if (!isFinal && _c.StepTxCount >= 1 + _settings.RetryLimit)
            {
                End(ContactOutcome.NoReply, nowUtc);
                return _c is null ? PlanTransmission(slotStartUtc, mode, nowUtc) : null;
            }
            if (isFinal && _c.StepTxCount >= 1) return null;
            _c.AwaitingReply = false;
            return new TxPlan(slotStartUtc, _c.Steps[_c.Phase].Message, _c.DxCall, _c.DxOffset);
        }

        if (_extraMessage is not null && parity == _extraParity)
        {
            return new TxPlan(slotStartUtc, _extraMessage, _lastLogged?.DxCall ?? _finished?.DxCall, null);
        }

        if (_cqRunning && parity == _cqParity) return new TxPlan(slotStartUtc, CqMessage(), null, null);
        return null;
    }

    /// <summary>Records that a planned message went out (time on, logging on RR73 in flow B, 73 sent once).</summary>
    public void OnTransmitted(TxPlan plan, DateTime startedUtc)
    {
        if (_extraMessage is not null && plan.Message == _extraMessage)
        {
            _extraMessage = null;
            return;
        }
        if (_c is null || plan.DxCall is null || !Callsign.EqualsCall(plan.DxCall, _c.DxCall)) return;
        var step = _c.Steps[_c.Phase];
        if (plan.Message != step.Message) return;
        _c.TimeOnUtc ??= startedUtc;
        _c.StepTxCount++;
        _c.AwaitingReply = true;
        _c.Steps[_c.Phase] = step with { Count = step.Count + 1, TimeUtc = startedUtc };

        if (_c.Flow == ContactFlow.CalledCq && _c.Phase == 4)
        {
            // Flow B: logged when RR73 has been transmitted. DX's 73 is optional and not waited for.
            _c.Steps[4] = _c.Steps[4] with { State = StepState.Done };
            if (_settings.AutoLog) DoLog(_c, startedUtc);
            End(ContactOutcome.Logged, startedUtc);
        }
        else if (IsFinalCourtesy(_c, _c.Phase))
        {
            _c.Steps[_c.Phase] = _c.Steps[_c.Phase] with { State = StepState.Done };
            End(ContactOutcome.Logged, startedUtc);
        }
    }

    /// <summary>Clears the ended contact from the top slot (one slot after it ended).</summary>
    public void ClearFinished() => _finished = null;

    private static bool IsFinalCourtesy(Contact c, int phase) => c.Flow != ContactFlow.CalledCq && phase == 4;

    private bool Advance(Contact c, ParsedMessage m, DateTime slotUtc, DateTime nowUtc)
    {
        if (c.Flow == ContactFlow.CalledCq)
        {
            switch (m.Kind)
            {
                case MessageKind.RogerReport when c.Phase == 2:
                    c.ReportReceived = m.Report;
                    MarkReceived(c, 3, m.Text, slotUtc);
                    SetPhase(c, 4);
                    return true;
                case MessageKind.RogerReport when c.Phase == 4:
                    Count(c, 3, slotUtc);
                    c.AwaitingReply = false;
                    return false;
                case MessageKind.GridReply when c.Phase == 2:
                    Count(c, 1, slotUtc); // they did not copy my report: resend it
                    c.AwaitingReply = false;
                    return false;
                default:
                    return false;
            }
        }

        // Flow A and flow B with the grid skipped.
        switch (m.Kind)
        {
            case MessageKind.Report when c.Phase == 0 && c.Flow == ContactFlow.AnswerCq:
                c.ReportReceived = m.Report;
                MarkReceived(c, 1, m.Text, slotUtc);
                SetPhase(c, 2);
                return true;
            case MessageKind.Report when c.Phase == 2:
                Count(c, 1, slotUtc); // they repeated their report: resend my R-report
                c.AwaitingReply = false;
                return false;
            case MessageKind.RogerBye or MessageKind.Roger or MessageKind.Bye when c.Phase == 2:
                MarkReceived(c, 3, m.Text, slotUtc);
                if (_settings.AutoLog) DoLog(c, nowUtc);
                SetPhase(c, 4);
                return true;
            case MessageKind.RogerBye or MessageKind.Roger or MessageKind.Bye when c.Phase == 4:
                Count(c, 3, slotUtc);
                if (c.StepTxCount >= 1)
                {
                    // A repeated sign-off after my 73: one more 73.
                    c.StepTxCount = 0;
                }
                return false;
            default:
                return false;
        }
    }

    private void LateSignoff(List<(HeardMessage H, ParsedMessage M)> toMe, DateTime slotUtc)
    {
        if (_lastLogged is not { } l || slotUtc - l.LoggedUtc >= LateWindow) return;
        foreach (var (_, m) in toMe)
        {
            if (!Callsign.EqualsCall(m.SenderCall, l.DxCall)) continue;
            if (l.Flow != ContactFlow.CalledCq && m.Kind is MessageKind.RogerBye or MessageKind.Bye or MessageKind.Roger)
            {
                _extraMessage = Format(l.Contact, 4);
                _extraParity = l.MyParity;
            }
            else if (l.Flow == ContactFlow.CalledCq && m.Kind == MessageKind.RogerReport)
            {
                _extraMessage = Format(l.Contact, 4);
                _extraParity = l.MyParity;
            }
        }
    }

    private void StartFromCaller(WaitingCaller w)
    {
        var parity = SlotMath.Opposite(w.Parity);
        if (w.Kind == MessageKind.Report)
        {
            var c = new Contact(ContactFlow.CalledCqSkippedGrid, w.Call, w.Grid, parity, Report.Clamp(w.Snr), w.OffsetHz) { ReportReceived = w.Report };
            c.Steps.AddRange(StepsFor(c));
            c.Steps[0] = c.Steps[0] with { State = StepState.Done };
            MarkReceived(c, 1, $"{_myCall} {Dx(w.Call)} {Report.Format(w.Report ?? 0)}", w.LastCalledUtc);
            SetPhase(c, 2);
            _c = c;
        }
        else
        {
            var c = new Contact(ContactFlow.CalledCq, w.Call, w.Grid, parity, Report.Clamp(w.Snr), w.OffsetHz);
            c.Steps.AddRange(StepsFor(c));
            c.Steps[0] = c.Steps[0] with { State = StepState.Done };
            MarkReceived(c, 1, $"{_myCall} {Dx(w.Call)} {w.Grid}".TrimEnd(), w.LastCalledUtc);
            SetPhase(c, 2);
            _c = c;
        }
        RemoveWaiting(w.Call);
    }

    private void End(ContactOutcome outcome, DateTime nowUtc, bool resume = true)
    {
        if (_c is null) return;
        _c.Outcome = outcome;
        if (outcome == ContactOutcome.Logged)
            _lastLogged = new Finished(_c.DxCall, _c.Flow, _c.MyParity, _c.LoggedUtc ?? nowUtc, _c);
        _finished = _c;
        _c = null;
        if (!resume) return;
        var next = _waiting.FirstOrDefault(w => w.AnswerAfter);
        if (next is not null) StartFromCaller(next);
    }

    private void DoLog(Contact c, DateTime nowUtc)
    {
        if (c.Logged) return;
        c.Logged = true;
        c.LoggedUtc = nowUtc;
        Logged?.Invoke(new ContactLogEntry(c.DxCall, c.DxGrid, c.ReportSent, c.ReportReceived, c.TimeOnUtc ?? nowUtc, nowUtc));
    }

    private static void SetPhase(Contact c, int phase)
    {
        if (c.Phase < c.Steps.Count && c.Steps[c.Phase].Count > 0) c.Steps[c.Phase] = c.Steps[c.Phase] with { State = StepState.Done };
        c.Phase = phase;
        c.StepTxCount = 0;
        c.AwaitingReply = false;
    }

    private static void MarkReceived(Contact c, int index, string text, DateTime utc) =>
        c.Steps[index] = c.Steps[index] with { Message = text, State = StepState.Done, Count = c.Steps[index].Count + 1, TimeUtc = utc };

    private static void Count(Contact c, int index, DateTime utc) =>
        c.Steps[index] = c.Steps[index] with { Count = c.Steps[index].Count + 1, TimeUtc = utc };

    private void AddWaiting(HeardMessage h, ParsedMessage m, SlotParity parity, DateTime slotUtc)
    {
        var call = m.SenderCall!;
        var i = _waiting.FindIndex(x => Callsign.EqualsCall(x.Call, call));
        if (i >= 0)
        {
            var w = _waiting[i];
            _waiting[i] = w with
            {
                LastCalledUtc = slotUtc, Snr = h.Decode.Snr, Parity = parity, OffsetHz = h.Decode.OffsetHz,
                Kind = m.Kind is MessageKind.GridReply or MessageKind.Report ? m.Kind : w.Kind,
                Grid = m.Grid ?? w.Grid, Report = m.Report ?? w.Report,
            };
            return;
        }
        if (_waiting.Count >= MaxWaiting) return;
        if (m.Kind is not (MessageKind.GridReply or MessageKind.Report or MessageKind.RogerReport or MessageKind.Bare)) return;
        _waiting.Add(new WaitingCaller(call, m.Grid, h.Decode.Snr, m.Kind == MessageKind.RogerReport ? MessageKind.Report : m.Kind,
            m.Report, parity, h.Decode.OffsetHz, slotUtc, slotUtc));
    }

    private void RemoveWaiting(string call) => _waiting.RemoveAll(x => Callsign.EqualsCall(x.Call, call));

    private string CqMessage() => _myGrid.Length > 0 ? $"CQ {_myCall} {_myGrid}" : $"CQ {_myCall}";

    private static string Dx(string call) => MessagePacker.IsStandardCall(call) ? call : $"<{call}>";

    private string Format(Contact c, int index) => c.Steps.Count > index ? c.Steps[index].Message : $"{Dx(c.DxCall)} {_myCall} 73";

    private List<ContactStep> StepsFor(Contact c)
    {
        var dx = Dx(c.DxCall);
        var me = _myCall;
        var rpt = Report.Format(c.ReportSent);
        var grid = _myGrid.Length > 0 ? $" {_myGrid}" : string.Empty;
        ContactStep Mine(string m) => new(true, m, StepState.Upcoming, 0, null);
        ContactStep Theirs(string m) => new(false, m, StepState.Upcoming, 0, null);
        return c.Flow switch
        {
            ContactFlow.AnswerCq =>
            [
                Mine($"{dx} {me}{grid}"), Theirs($"{me} {dx} ±NN"), Mine($"{dx} {me} R{rpt}"), Theirs($"{me} {dx} RR73"), Mine($"{dx} {me} 73"),
            ],
            ContactFlow.CalledCq =>
            [
                Mine($"CQ {me}{grid}"), Theirs($"{me} {dx} {c.DxGrid}".TrimEnd()), Mine($"{dx} {me} {rpt}"), Theirs($"{me} {dx} R±NN"),
                Mine($"{dx} {me} {_settings.Signoff}"), Theirs($"{me} {dx} 73"),
            ],
            _ =>
            [
                Mine($"CQ {me}{grid}"), Theirs($"{me} {dx} ±NN"), Mine($"{dx} {me} R{rpt}"), Theirs($"{me} {dx} RR73"), Mine($"{dx} {me} 73"),
            ],
        };
    }

    private ContactView ViewOf(Contact c)
    {
        var steps = new List<ContactStep>(c.Steps.Count);
        for (var i = 0; i < c.Steps.Count; i++)
        {
            var s = c.Steps[i];
            if (c.Outcome == ContactOutcome.InProgress)
            {
                var nowIndex = c.AwaitingReply && i == c.Phase + 1 ? i : !c.AwaitingReply && i == c.Phase ? i : -1;
                if (nowIndex == i) s = s with { State = StepState.Now };
                else if (i == c.Phase && c.AwaitingReply && s.Count > 0) s = s with { State = StepState.Done };
            }
            else if (i == c.Phase && s.Count > 0)
            {
                s = s with { State = StepState.Done };
            }
            steps.Add(s);
        }
        var sentAny = c.Steps.Any(s => s.Mine && s.Count > 0 && s.Message.Contains(Report.Format(c.ReportSent), StringComparison.Ordinal));
        var status = c.Outcome switch
        {
            ContactOutcome.InProgress when c.AwaitingReply => $"Waiting for {c.DxCall}.",
            ContactOutcome.InProgress => $"Transmitting to {c.DxCall}.",
            ContactOutcome.Logged => $"Logged {c.DxCall}.",
            ContactOutcome.NoReply => $"No reply from {c.DxCall}.",
            ContactOutcome.Watchdog => "Stopped by watchdog.",
            _ => $"Stopped calling {c.DxCall}.",
        };
        return new ContactView(c.DxCall, c.DxGrid, c.Flow, c.Outcome, sentAny ? c.ReportSent : null, c.ReportReceived, steps, c.Logged,
            !c.Logged && sentAny && c.ReportReceived is not null, status);
    }

    private sealed class Contact(ContactFlow flow, string dxCall, string? dxGrid, SlotParity myParity, int reportSent, int dxOffset)
    {
        public ContactFlow Flow { get; } = flow;

        public string DxCall { get; } = dxCall;

        public string? DxGrid { get; } = dxGrid;

        public SlotParity MyParity { get; } = myParity;

        public int ReportSent { get; } = reportSent;

        public int DxOffset { get; } = dxOffset;

        public int? ReportReceived { get; set; }

        public List<ContactStep> Steps { get; } = [];

        public int Phase { get; set; }

        public int StepTxCount { get; set; }

        public bool AwaitingReply { get; set; }

        public DateTime? TimeOnUtc { get; set; }

        public bool Logged { get; set; }

        public DateTime? LoggedUtc { get; set; }

        public ContactOutcome Outcome { get; set; } = ContactOutcome.InProgress;
    }

    private sealed record Finished(string DxCall, ContactFlow Flow, SlotParity MyParity, DateTime LoggedUtc, Contact Contact);

    private string Format(Finished f, int index) => Format(f.Contact, index);
}
