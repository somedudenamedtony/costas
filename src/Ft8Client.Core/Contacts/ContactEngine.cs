// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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

    /// <summary>How long a caller stays in Waiting for you, and how long a stopped contact can be resumed.</summary>
    public static readonly TimeSpan WaitingWindow = TimeSpan.FromMinutes(5);

    /// <summary>How long a Hound keeps calling a Fox that has not answered (as in WSJT-X, which stops after 2 minutes).</summary>
    public static readonly TimeSpan HoundCallLimit = TimeSpan.FromMinutes(2);

    /// <summary>Slots a Hound keeps sending its R-report without the Fox's RR73 (WSJT-X's timeout of 11 slots).</summary>
    public const int HoundReportSlots = 11;

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
    private Stopped? _stopped;
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

    /// <summary>True while a <see cref="ContactFlow.Hound"/> contact (with a Fox) is in progress.</summary>
    public bool InHoundContact => _c?.Flow == ContactFlow.Hound;

    /// <summary>
    /// Tells the engine which stations run Fox/Hound, so a Fox's report that the operator answers from Waiting for you
    /// continues as a Hound contact. Null treats no station as a Fox.
    /// </summary>
    public Func<string, bool>? IsFox { get; set; }

    /// <summary>The DX station's grid, if known.</summary>
    public string? DxGrid => _c?.DxGrid;

    /// <summary>QSO progress 0 to 5 for jt9 a-priori decoding.</summary>
    public int QsoProgress => _c is null ? 0 : (_c.Flow, _c.Phase) switch
    {
        (ContactFlow.AnswerCq or ContactFlow.Hound, 0) => 1,
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

    /// <summary>
    /// Flow A: call a station (answering its CQ, or replying to its report if it called me first). When
    /// <paramref name="busyWith"/> names another station it is working right now, nothing is sent: the contact ends at
    /// once as <see cref="ContactOutcome.AnsweredOther"/> so the operator sees why. With <paramref name="fox"/> the
    /// station runs Fox/Hound: it is called as a Hound (<see cref="ContactFlow.Hound"/>), and since a Fox is always working
    /// someone, <paramref name="busyWith"/> is ignored.
    /// </summary>
    public void CallStation(string call, string? grid, int snr, SlotParity dxParity, int dxOffsetHz, DateTime nowUtc, string? busyWith = null,
                            bool fox = false)
    {
        OperatorActivity(nowUtc);
        call = Callsign.Normalize(call);
        _finished = null;
        _extraMessage = null;
        var flow = fox ? ContactFlow.Hound : ContactFlow.AnswerCq;
        _c = new Contact(flow, call, Grid.Grid4(grid), SlotMath.Opposite(dxParity), Report.Clamp(snr), dxOffsetHz);
        _c.Steps.AddRange(StepsFor(_c));
        RemoveWaiting(call);
        if (_stopped is { } s && Callsign.EqualsCall(s.Contact.DxCall, call)) ForgetStopped();
        if (busyWith is not null && !fox)
        {
            _c.OtherCall = Callsign.Normalize(busyWith);
            _c.BusyAtStart = true;
            End(ContactOutcome.AnsweredOther, nowUtc, resume: false);
        }
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

    /// <summary>
    /// Answers a waiting caller now: report to a grid, R-report to a report. A station that replied after I stopped its
    /// contact resumes that contact, applying what it sent since.
    /// </summary>
    public bool AnswerCaller(string call, DateTime nowUtc)
    {
        OperatorActivity(nowUtc);
        var w = _waiting.FirstOrDefault(x => Callsign.EqualsCall(x.Call, call));
        if (w is null) return false;
        _finished = null;
        return Begin(w, nowUtc);
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
        else if (_finished is { } f && f.Logged && f.Flow != ContactFlow.Hound)
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
            foreach (var part in h.Message.Expand())
            {
                var m = FromFox(h, part);
                if (m.SenderCall is null || !m.IsAddressedTo(_myCall)) continue;
                if (m.To is { } t && !Callsign.EqualsCall(t.Call, _myCall)) continue;
                toMe.Add((h, m));
            }
        }

        if (_c is not null)
        {
            foreach (var (h, m) in toMe.Where(x => Callsign.EqualsCall(x.M.SenderCall, _c.DxCall)))
            {
                Advance(_c, m, slotStartUtc, nowUtc, h);
                if (_c is null) break;
            }
        }
        else
        {
            LateSignoff(toMe, slotStartUtc);
        }

        // I answered its CQ and it has not replied to me, but it was heard working someone else: it picked another
        // caller, so stop calling it rather than keep sending into its contact.
        if (_c is { Flow: ContactFlow.AnswerCq, Phase: 0, ReportReceived: null } a && AnsweredOther(a, heard) is { } other)
        {
            a.OtherCall = other;
            End(ContactOutcome.AnsweredOther, nowUtc);
        }

        // Answer the first caller to my CQ.
        if (_c is null && _cqRunning && parity != _cqParity)
        {
            // A caller with a compound call answers with its calls only (a type 4 message has no room for a grid).
            var first = toMe.FirstOrDefault(x => x.M.Kind is MessageKind.GridReply or MessageKind.Report or MessageKind.Bare);
            if (first.M is not null)
            {
                // Past the cap too: a caller to my CQ is answered even when the waiting list is full.
                AddWaiting(first.H, first.M, parity, slotStartUtc, ignoreCap: true);
                var w = _waiting.First(x => Callsign.EqualsCall(x.Call, first.M.SenderCall));
                StartFromCaller(w);
            }
        }

        if (_stopped is { } old && nowUtc - old.EndedUtc > WaitingWindow) ForgetStopped();
        foreach (var (h, m) in toMe)
        {
            if (_c is not null && Callsign.EqualsCall(m.SenderCall, _c.DxCall)) continue;
            if (_lastLogged is { } l && Callsign.EqualsCall(m.SenderCall, l.DxCall) && nowUtc - l.LoggedUtc < LateWindow) continue;
            if (_stopped is { } s && Callsign.EqualsCall(m.SenderCall, s.Contact.DxCall))
            {
                s.Late.Add((m, slotStartUtc, h));
                if (_c is null && AnsweredLastTry(s, m, slotStartUtc, mode))
                {
                    // Its answer to my last try was decoded only after my next slot began, when the retry limit had
                    // already ended the contact. Had it been decoded in time the contact would have gone on: so it does.
                    Resume(s, nowUtc);
                    continue;
                }
                AddWaiting(h, m, parity, slotStartUtc, afterStop: true);
                continue;
            }
            AddWaiting(h, m, parity, slotStartUtc);
        }
        _waiting.RemoveAll(w => nowUtc - w.LastCalledUtc > WaitingWindow);
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
            if (_c.Flow == ContactFlow.Hound) return PlanHound(_c, slotStartUtc, mode, nowUtc);
            var isFinal = IsFinalCourtesy(_c, _c.Phase);
            if (!isFinal && _c.StepTxCount >= 1 + _settings.RetryLimit)
            {
                // Ended as of this slot, so a late decode of the other station's last slot can still pick it up.
                End(ContactOutcome.NoReply, slotStartUtc);
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
        if (_c.StepTxCount == 0) _c.StepStartedUtc = startedUtc;
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
            var parity = _c.MyParity;
            End(ContactOutcome.Logged, startedUtc);
            if (_c is null && OutgoingMessages.FreeTextAfter(_settings) is { } free)
            {
                // The operator's free text goes once, in my next slot, unless another contact has started.
                _extraMessage = free;
                _extraParity = parity;
            }
        }
    }

    /// <summary>Clears the ended contact from the top slot (one slot after it ended).</summary>
    public void ClearFinished() => _finished = null;

    private static bool IsFinalCourtesy(Contact c, int phase) => c.Flow != ContactFlow.CalledCq && phase == 4;

    private bool Advance(Contact c, ParsedMessage m, DateTime slotUtc, DateTime nowUtc, HeardMessage? heard = null)
    {
        if (c.Flow == ContactFlow.Hound) return AdvanceHound(c, m, slotUtc, nowUtc, heard);
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

    // A Hound (WSJT-X mainwindow.cpp, hound_reply and the RR73 check before it): the Fox's report, alone or in a
    // multi-answer message, moves me to my R-report on the frequency it answered on, with its SNR as my report; a
    // repeated report keeps me sending it; RR73 logs and ends the contact with nothing more sent.
    private bool AdvanceHound(Contact c, ParsedMessage m, DateTime slotUtc, DateTime nowUtc, HeardMessage? heard)
    {
        switch (m.Kind)
        {
            case MessageKind.Report when c.Phase is 0 or 2:
                c.ReportReceived = m.Report;
                c.ReportReceivedUtc = slotUtc;
                if (heard is not null)
                {
                    c.TxOffset = heard.Decode.OffsetHz;
                    c.ReportSent = Report.Clamp(heard.Decode.Snr);
                }
                c.Steps[2] = c.Steps[2] with { Message = OutgoingMessages.Report(Calls(c.DxCall), c.ReportSent, roger: true) };
                if (c.Phase == 0)
                {
                    MarkReceived(c, 1, m.Text, slotUtc);
                    SetPhase(c, 2);
                    return true;
                }
                Count(c, 1, slotUtc); // it did not copy my R-report: keep sending it
                c.AwaitingReply = false;
                return false;
            case MessageKind.RogerBye when c.Phase == 2:
                MarkReceived(c, 3, m.Text, slotUtc);
                if (_settings.AutoLog) DoLog(c, nowUtc);
                c.Steps[2] = c.Steps[2] with { State = StepState.Done };
                c.Phase = 3;
                End(ContactOutcome.Logged, nowUtc, resume: false);
                return true;
            default:
                return false;
        }
    }

    // Calling: up to HoundCallLimit from my first call. R-report: up to HoundReportSlots from the Fox's latest report.
    private TxPlan? PlanHound(Contact c, DateTime slotStartUtc, Mode mode, DateTime nowUtc)
    {
        var expired = c.Phase == 0
            ? c.StepStartedUtc is { } first && slotStartUtc - first >= HoundCallLimit
            : c.ReportReceivedUtc is { } rpt && slotStartUtc - rpt >= HoundReportSlots * ModeInfo.SlotLength(mode);
        if (expired)
        {
            End(ContactOutcome.NoReply, slotStartUtc, resume: false);
            return null;
        }
        c.AwaitingReply = false;
        return new TxPlan(slotStartUtc, c.Steps[c.Phase].Message, c.DxCall, c.DxOffset, c.Phase == 2 ? c.TxOffset : null);
    }

    // In a Hound contact, a multi-answer message whose sender hash did not resolve is the Fox's: it is the station I am
    // working, and WSJT-X also reads these by position without checking the Fox's call.
    private ParsedMessage FromFox(HeardMessage h, ParsedMessage m) =>
        _c is { Flow: ContactFlow.Hound } c && h.Message.Kind == MessageKind.DxpeditionMulti && m.From is { Unresolved: true }
            ? m with { From = new CallToken(c.DxCall, Hashed: true, Unresolved: false) }
            : m;

    // The call the DX station started a contact with in these decodes, when it is not me.
    private string? AnsweredOther(Contact c, IReadOnlyList<HeardMessage> heard)
    {
        string? other = null;
        foreach (var h in heard)
        {
            if (h.Decode.LowConfidence) continue;
            foreach (var m in h.Message.Expand())
            {
                if (!Callsign.EqualsCall(m.SenderCall, c.DxCall)) continue;
                if (m.IsAddressedTo(_myCall)) return null; // it is also working me (a multi-stream DXpedition, say)
                // A report or a grid starts a contact; a sign-off (RR73, 73) only finishes an earlier one.
                if (m.To is { Unresolved: false } to && m.Kind is MessageKind.Report or MessageKind.RogerReport or MessageKind.GridReply)
                    other ??= to.Call;
            }
        }
        return other;
    }

    private void LateSignoff(List<(HeardMessage H, ParsedMessage M)> toMe, DateTime slotUtc)
    {
        if (_lastLogged is not { } l || slotUtc - l.LoggedUtc >= LateWindow) return;
        foreach (var (_, m) in toMe)
        {
            if (!Callsign.EqualsCall(m.SenderCall, l.DxCall) || l.Flow == ContactFlow.Hound) continue;
            // After my RR73, their 73 is the normal answer, not a repeat.
            var signedOffRr73 = Format(l.Contact, 4).EndsWith(" RR73", StringComparison.Ordinal);
            if (l.Flow != ContactFlow.CalledCq && (m.Kind is MessageKind.RogerBye or MessageKind.Roger || (m.Kind == MessageKind.Bye && !signedOffRr73)))
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
        _extraMessage = null;
        if (w.Kind == MessageKind.Report && IsFox?.Invoke(w.Call) == true)
        {
            // A Fox's report with no Hound contact running (I stopped calling it): my R-report on its frequency.
            var c = new Contact(ContactFlow.Hound, w.Call, w.Grid, parity, Report.Clamp(w.Snr), w.OffsetHz)
            {
                ReportReceived = w.Report, ReportReceivedUtc = w.LastCalledUtc, TxOffset = w.OffsetHz,
            };
            c.Steps.AddRange(StepsFor(c));
            c.Steps[0] = c.Steps[0] with { State = StepState.Done };
            MarkReceived(c, 1, $"{_myCall} {Dx(w.Call)} {Report.Format(w.Report ?? 0)}", w.LastCalledUtc);
            SetPhase(c, 2);
            _c = c;
        }
        else if (w.Kind == MessageKind.Report)
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
        if (_stopped is { } s && Callsign.EqualsCall(s.Contact.DxCall, w.Call)) ForgetStopped();
    }

    private bool Begin(WaitingCaller w, DateTime nowUtc)
    {
        if (w.AfterStop && _stopped is { } s && Callsign.EqualsCall(s.Contact.DxCall, w.Call))
        {
            Resume(s, nowUtc);
            return true;
        }
        if (w.Kind is not (MessageKind.GridReply or MessageKind.Report or MessageKind.Bare)) return false;
        StartFromCaller(w);
        return true;
    }

    // Picks a stopped contact up where it ended and applies what the station sent since, as if it had arrived in time.
    private void Resume(Stopped s, DateTime nowUtc)
    {
        var c = s.Contact;
        _stopped = null;
        if (ReferenceEquals(_finished, c)) _finished = null;
        c.Outcome = ContactOutcome.InProgress;
        c.StepTxCount = 0;
        c.AwaitingReply = false;
        _extraMessage = null; // a free text or extra 73 queued meanwhile belonged to another station
        _c = c;
        foreach (var (m, slot, heard) in s.Late)
        {
            Advance(c, m, slot, nowUtc, heard);
            if (_c is null) break;
        }
        RemoveWaiting(c.DxCall);
    }

    // The contact ended for no reply at the start of my slot that follows this decoded slot, and the message moves it
    // on (not a repeat, which would not have kept it going either).
    private static bool AnsweredLastTry(Stopped s, ParsedMessage m, DateTime slotUtc, Mode mode)
    {
        var c = s.Contact;
        if (c.Outcome != ContactOutcome.NoReply || slotUtc + ModeInfo.SlotLength(mode) != SlotMath.SlotStart(s.EndedUtc, mode)) return false;
        return (c.Flow, c.Phase, m.Kind) switch
        {
            (ContactFlow.AnswerCq or ContactFlow.Hound, 0, MessageKind.Report) => true,
            (ContactFlow.CalledCq, 2, MessageKind.RogerReport) => true,
            (ContactFlow.Hound, 2, MessageKind.RogerBye) => true,
            (ContactFlow.AnswerCq or ContactFlow.CalledCqSkippedGrid, 2, MessageKind.RogerBye or MessageKind.Roger or MessageKind.Bye) => true,
            _ => false,
        };
    }

    // A stopped contact can no longer be resumed: its late replies become ordinary callers, or go if they are sign-offs.
    private void ForgetStopped()
    {
        if (_stopped is not { } s) return;
        _stopped = null;
        var i = _waiting.FindIndex(x => x.AfterStop && Callsign.EqualsCall(x.Call, s.Contact.DxCall));
        if (i < 0) return;
        if (_waiting[i].Kind is MessageKind.GridReply or MessageKind.Report or MessageKind.Bare) _waiting[i] = _waiting[i] with { AfterStop = false };
        else _waiting.RemoveAt(i);
    }

    private void End(ContactOutcome outcome, DateTime nowUtc, bool resume = true)
    {
        if (_c is null) return;
        _c.Outcome = outcome;
        if (outcome == ContactOutcome.Logged)
        {
            _lastLogged = new Finished(_c.DxCall, _c.Flow, _c.MyParity, _c.LoggedUtc ?? nowUtc, _c);
        }
        else
        {
            ForgetStopped();
            _stopped = new Stopped(_c, nowUtc, []);
        }
        _finished = _c;
        _c = null;
        if (!resume) return;
        var next = _waiting.FirstOrDefault(w => w.AnswerAfter);
        if (next is not null) Begin(next, nowUtc);
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
        c.StepStartedUtc = null;
        c.AwaitingReply = false;
    }

    private static void MarkReceived(Contact c, int index, string text, DateTime utc) =>
        c.Steps[index] = c.Steps[index] with { Message = text, State = StepState.Done, Count = c.Steps[index].Count + 1, TimeUtc = utc };

    private static void Count(Contact c, int index, DateTime utc) =>
        c.Steps[index] = c.Steps[index] with { Count = c.Steps[index].Count + 1, TimeUtc = utc };

    // A station replying after I stopped its contact is always kept, whatever it sent, so the operator sees it.
    private void AddWaiting(HeardMessage h, ParsedMessage m, SlotParity parity, DateTime slotUtc, bool afterStop = false, bool ignoreCap = false)
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
                AfterStop = w.AfterStop || afterStop, LastMessage = m.Text,
            };
            return;
        }
        if (!afterStop && !ignoreCap && _waiting.Count >= MaxWaiting) return;
        if (!afterStop && m.Kind is not (MessageKind.GridReply or MessageKind.Report or MessageKind.RogerReport or MessageKind.Bare)) return;
        _waiting.Add(new WaitingCaller(call, m.Grid, h.Decode.Snr, m.Kind == MessageKind.RogerReport ? MessageKind.Report : m.Kind,
            m.Report, parity, h.Decode.OffsetHz, slotUtc, slotUtc, AfterStop: afterStop, LastMessage: m.Text));
    }

    private void RemoveWaiting(string call) => _waiting.RemoveAll(x => Callsign.EqualsCall(x.Call, call));

    private string CqMessage() => OutgoingMessages.Cq(_myCall, _myGrid, _settings);

    // A nonstandard DX call as the received-message placeholders show it.
    private static string Dx(string call) => MessagePacker.IsStandardCall(call) ? call : $"<{call}>";

    private CallPair Calls(string dxCall) => CallPair.Of(dxCall, _myCall);

    private string Format(Contact c, int index) => c.Steps.Count > index ? c.Steps[index].Message : OutgoingMessages.Final(Calls(c.DxCall), _settings);

    private List<ContactStep> StepsFor(Contact c)
    {
        var dx = Dx(c.DxCall);
        var me = _myCall;
        var calls = Calls(c.DxCall);
        var cq = CqMessage();
        var final = OutgoingMessages.Final(calls, _settings);
        ContactStep Mine(string m) => new(true, m, StepState.Upcoming, 0, null);
        ContactStep Theirs(string m) => new(false, m, StepState.Upcoming, 0, null);
        return c.Flow switch
        {
            ContactFlow.AnswerCq =>
            [
                Mine(OutgoingMessages.Reply(calls, _myGrid, _settings)), Theirs($"{me} {dx} ±NN"), Mine(OutgoingMessages.Report(calls, c.ReportSent, roger: true)),
                Theirs($"{me} {dx} RR73"), Mine(final),
            ],
            ContactFlow.Hound =>
            [
                Mine(OutgoingMessages.Reply(calls, _myGrid, _settings)), Theirs($"{me} {dx} ±NN"), Mine(OutgoingMessages.Report(calls, c.ReportSent, roger: true)),
                Theirs($"{me} {dx} RR73"),
            ],
            ContactFlow.CalledCq =>
            [
                Mine(cq), Theirs($"{me} {dx} {c.DxGrid}".TrimEnd()), Mine(OutgoingMessages.Report(calls, c.ReportSent)), Theirs($"{me} {dx} R±NN"),
                Mine(OutgoingMessages.Signoff(calls, _settings.Signoff)), Theirs($"{me} {dx} 73"),
            ],
            _ =>
            [
                Mine(cq), Theirs($"{me} {dx} ±NN"), Mine(OutgoingMessages.Report(calls, c.ReportSent, roger: true)), Theirs($"{me} {dx} RR73"), Mine(final),
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
            ContactOutcome.NoReply when c.Flow == ContactFlow.Hound && c.Phase == 0 =>
                $"{c.DxCall} did not answer in {HoundCallLimit.TotalMinutes:0} minutes. Call it again to keep trying.",
            ContactOutcome.NoReply => $"No reply from {c.DxCall}.",
            ContactOutcome.Watchdog => "Stopped by watchdog.",
            ContactOutcome.AnsweredOther when c.BusyAtStart => $"{c.DxCall} is in a contact with {c.OtherCall}. Not calling.",
            ContactOutcome.AnsweredOther => c.OtherCall is null ? $"{c.DxCall} answered someone else. Stopped calling." : $"{c.DxCall} answered {c.OtherCall} instead. Stopped calling.",
            _ => $"Stopped calling {c.DxCall}.",
        };
        return new ContactView(c.DxCall, c.DxGrid, c.Flow, c.Outcome, sentAny ? c.ReportSent : null, c.ReportReceived, steps, c.Logged,
            !c.Logged && sentAny && c.ReportReceived is not null, status) { MyParity = c.MyParity };
    }

    private sealed class Contact(ContactFlow flow, string dxCall, string? dxGrid, SlotParity myParity, int reportSent, int dxOffset)
    {
        public ContactFlow Flow { get; } = flow;

        public string DxCall { get; } = dxCall;

        public string? DxGrid { get; } = dxGrid;

        public SlotParity MyParity { get; } = myParity;

        public int ReportSent { get; set; } = reportSent;

        public int DxOffset { get; } = dxOffset;

        public int? ReportReceived { get; set; }

        public DateTime? ReportReceivedUtc { get; set; }

        public int? TxOffset { get; set; }

        public DateTime? StepStartedUtc { get; set; }

        public List<ContactStep> Steps { get; } = [];

        public int Phase { get; set; }

        public int StepTxCount { get; set; }

        public bool AwaitingReply { get; set; }

        public DateTime? TimeOnUtc { get; set; }

        public bool Logged { get; set; }

        public DateTime? LoggedUtc { get; set; }

        public ContactOutcome Outcome { get; set; } = ContactOutcome.InProgress;

        public string? OtherCall { get; set; }

        public bool BusyAtStart { get; set; }
    }

    private sealed record Finished(string DxCall, ContactFlow Flow, SlotParity MyParity, DateTime LoggedUtc, Contact Contact);

    private sealed record Stopped(Contact Contact, DateTime EndedUtc, List<(ParsedMessage Message, DateTime SlotUtc, HeardMessage Heard)> Late);

    private string Format(Finished f, int index) => Format(f.Contact, index);
}
