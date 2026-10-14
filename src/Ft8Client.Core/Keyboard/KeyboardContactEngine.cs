// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Keyboard;

/// <summary>
/// Runs a PSK or RTTY contact on the same five-step strip as FT8 (docs/09-keyboard-modes.md section 7). The operator
/// sends; this only follows. My steps are done when their macro has been sent; the partner's steps are done when their
/// text has what the step needs. The exchange (report, name, QTH, grid) is read from their text, and a value the
/// operator set by clicking a word always wins. Logged when I send 73 after my CQ, or when they send 73 after I
/// answered theirs, or on Log now. Not thread-safe; the session calls it under its lock.
/// </summary>
public sealed partial class KeyboardContactEngine
{
    /// <summary>The report sent when the operator has not set one.</summary>
    public const int DefaultReport = 599;

    private readonly Step[] _steps = new Step[5];
    private readonly StringBuilder _heardSinceMine = new();
    private readonly Dictionary<ExchangeField, string> _captured = [];
    private string _myCall;
    private string? _partner;
    private int _partnerOffset;
    private ContactFlow _flow;
    private ContactOutcome _outcome;
    private Exchange _received = Exchange.None;
    private DateTime? _timeOn;
    private bool _logged;
    private bool _reportSent;
    private bool _heardToMe;
    private DateTime? _tailEndersUntil;

    // Their text kept since my last over, read as a whole so an exchange split across lines is found.
    private const int MaxHeard = 2000;

    /// <summary>How long after a logged contact a station calling me (a tail-ender) starts a new contact.</summary>
    public static readonly TimeSpan TailEnderWindow = TimeSpan.FromMinutes(2);

    /// <summary>Creates an engine for my call.</summary>
    public KeyboardContactEngine(string myCall) => _myCall = Callsign.Normalize(myCall);

    /// <summary>Raised when a contact is logged.</summary>
    public event Action<KeyboardLogEntry>? Logged;

    /// <summary>True from when a CQ is sent until a station answers it or the operator stops.</summary>
    public bool CallingCq { get; private set; }

    /// <summary>The report I send: 599 unless the operator changes it.</summary>
    public int ReportToSend { get; set; } = DefaultReport;

    /// <summary>The partner's call, or null with no contact.</summary>
    public string? Partner => _partner;

    /// <summary>The partner's audio offset.</summary>
    public int PartnerOffsetHz => _partnerOffset;

    /// <summary>True while a contact is under way (not yet logged or ended).</summary>
    public bool InContact => _partner is not null && _outcome == ContactOutcome.InProgress;

    /// <summary>The exchange so far: values the operator clicked, else those read from the partner's text.</summary>
    public Exchange Received => new(
        _captured.TryGetValue(ExchangeField.Rst, out var r) && int.TryParse(r, NumberStyles.None, CultureInfo.InvariantCulture, out var rst) ? rst : _received.Rst,
        _captured.GetValueOrDefault(ExchangeField.Name) ?? _received.Name,
        _captured.GetValueOrDefault(ExchangeField.Qth) ?? _received.Qth,
        _captured.GetValueOrDefault(ExchangeField.Grid) ?? _received.Grid);

    /// <summary>The function key of my next step, for Enter and the highlighted button; null when none is next.</summary>
    public int? NextMacro
    {
        get
        {
            if (_partner is null || _outcome != ContactOutcome.InProgress) return CallingCq ? null : Macro.CqKey;
            foreach (var s in _steps)
            {
                if (s.State == StepState.Done) continue;
                if (s.Mine) return s.Key;
            }
            return null;
        }
    }

    /// <summary>Changes my call (after settings change).</summary>
    public void Configure(string myCall) => _myCall = Callsign.Normalize(myCall);

    /// <summary>
    /// A CQ went out (the CQ macro was sent): wait for a station to answer. An unfinished contact ends without being
    /// logged (Log now keeps it), so the stations that answer the CQ are heard.
    /// </summary>
    public void CqSent(DateTime utc)
    {
        if (InContact) _outcome = ContactOutcome.Abandoned;
        CallingCq = true;
        _tailEndersUntil = null;
        _timeOn = utc;
    }

    /// <summary>Stops waiting for answers to my CQ.</summary>
    public void StopCq() => CallingCq = false;

    /// <summary>
    /// Starts a contact with a station whose CQ I am answering. <paramref name="theirText"/> is what they last sent,
    /// shown as the first step.
    /// </summary>
    public void Answer(string call, string? grid, int offsetHz, string? theirText, DateTime utc)
    {
        CallingCq = false;
        Begin(call, offsetHz, ContactFlow.AnswerCq);
        _steps[0] = new Step(false, "Their CQ", theirText ?? "CQ", StepState.Done, utc, 0);
        _steps[1] = new Step(true, "Answer", "Answer", StepState.Now, null, Macro.AnswerKey);
        _steps[2] = new Step(false, "Their exchange", "Their exchange", StepState.Upcoming, null, 0);
        _steps[3] = new Step(true, "Exchange", "Exchange", StepState.Upcoming, null, Macro.ExchangeKey);
        _steps[4] = new Step(false, "73", "73", StepState.Upcoming, null, 0);
        if (grid is not null) _received = _received with { Grid = grid };
    }

    /// <summary>
    /// An over (or part of one) I sent has gone out. <paramref name="macroKey"/> is the macro it ended with, if any; it
    /// completes my step that sends that macro.
    /// </summary>
    public void Sent(int? macroKey, string text, DateTime utc)
    {
        if (macroKey == Macro.CqKey)
        {
            CqSent(utc);
            return;
        }
        if (!InContact) return;
        _timeOn ??= utc;
        _heardSinceMine.Clear();
        _heardToMe = false;
        if (macroKey == Macro.ExchangeKey) _reportSent = true;
        var i = Array.FindIndex(_steps, s => s.Mine && s.State != StepState.Done && s.Key == macroKey);
        if (i >= 0)
        {
            _steps[i] = _steps[i] with { State = StepState.Done, Message = text.Trim(), TimeUtc = utc, Count = 1 };
            Advance();
        }
        // I close the contact with my 73: after my CQ that is the last step; after answering their CQ (where 73 is not
        // one of my steps), both exchanges are done and their 73 is only a courtesy.
        if (macroKey == Macro.SignOffKey && (_steps.All(s => s.State == StepState.Done) || ExchangesDone())) Log(utc);
    }

    /// <summary>
    /// Text read from a channel. While calling CQ, or for a short while after a contact is logged (a tail-ender), text
    /// with my call from another station starts a contact with it. In a contact, only the partner's text counts: its
    /// <paramref name="sender"/>, or with no sender known, text on its offset that names me. Their text to another
    /// station (<paramref name="to"/>, the station the over is sent to, or an <c>A DE B</c> in the text) is not
    /// their part of this contact. Returns true when the text is the partner's, for the conversation box.
    /// </summary>
    public bool Heard(string text, string? sender, int offsetHz, DateTime utc, string? to = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var upper = text.ToUpperInvariant();
        var toMe = Words().Split(upper).Any(IsMe) || (to is not null && IsMe(to));
        var addressee = to ?? OverReader.Read(upper + " ").To;
        if (_partner is null || _outcome != ContactOutcome.InProgress)
        {
            if (CanStart(sender, toMe, addressee, utc))
            {
                var cq = CallingCq;
                var timeOn = cq ? _timeOn ?? utc : utc;
                Begin(sender!, offsetHz, ContactFlow.CalledCq);
                _timeOn = timeOn;
                _steps[0] = new Step(true, "CQ", cq ? "CQ" : "Tail-ender", StepState.Done, timeOn, Macro.CqKey);
                _steps[1] = new Step(false, "Their call", upper.Trim(), StepState.Done, utc, 0);
                _steps[2] = new Step(true, "Exchange", "Exchange", StepState.Now, null, Macro.ExchangeKey);
                _steps[3] = new Step(false, "Their exchange", "Their exchange", StepState.Upcoming, null, 0);
                _steps[4] = new Step(true, "73", "73", StepState.Upcoming, null, Macro.SignOffKey);
                CallingCq = false;
                Append(upper);
                _heardToMe = true;
                _received = ExchangeReader.Read(_heardSinceMine.ToString());
                return true;
            }
            return _partner is not null && FromPartner(sender, toMe, offsetHz);
        }
        if (!FromPartner(sender, toMe, offsetHz)) return false;
        _partnerOffset = offsetHz;
        if (addressee is not null && !IsMe(addressee)) return true;
        Append(upper);
        _heardToMe |= toMe;
        var since = _heardSinceMine.ToString();
        _received = ExchangeReader.Read(since).Over(_received);
        var i = Array.FindIndex(_steps, s => s.State != StepState.Done);
        if (i < 0 || _steps[i].Mine) return true;
        var step = _steps[i];
        var sinceWords = Words().Split(since);
        var complete = step.Label switch
        {
            "Their exchange" => ExchangeReader.Read(since).Rst is not null
                                || (_heardToMe && sinceWords.Any(w => w is "K" or "KN" or "BK")),
            // TU alone is often "thank you" in passing; only 73 or SK closes the contact.
            "73" => sinceWords.Any(w => w is "73" or "SK"),
            _ => false,
        };
        if (!complete) return true;
        _steps[i] = step with { State = StepState.Done, Message = Shorten(since), TimeUtc = utc, Count = 1 };
        Advance();
        if (step.Label == "73" && _flow == ContactFlow.AnswerCq) Log(utc);
        return true;
    }

    /// <summary>
    /// Sets a part of the exchange from a word the operator clicked. It beats anything read from the text. Returns
    /// false, changing nothing, when the word cannot be that part: a report that is not an RST (5NN counts as 599), a
    /// grid that is not one, or a call that is not one.
    /// </summary>
    public bool Capture(ExchangeField field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = value.Trim().ToUpperInvariant();
        if (value.Length == 0) return false;
        switch (field)
        {
            case ExchangeField.Call:
                if (!OverReader.IsCall(value)) return false;
                if (_partner is not null) _partner = value;
                return true;
            case ExchangeField.Rst:
                value = value.Replace('N', '9');
                if (!RstWord().IsMatch(value)) return false;
                break;
            case ExchangeField.Grid:
                if (!Grid.IsGrid4Or6(value)) return false;
                value = value.Length > 4 ? value[..4] + value[4..].ToLowerInvariant() : value;
                break;
        }
        _captured[field] = field is ExchangeField.Name or ExchangeField.Qth ? Capitalise(value) : value;
        return true;
    }

    /// <summary>Logs the contact now. An abandoned contact is not logged.</summary>
    public void LogNow(DateTime utc)
    {
        if (_partner is null || _logged || _outcome == ContactOutcome.Abandoned) return;
        Log(utc);
    }

    /// <summary>Ends the contact without logging (Esc).</summary>
    public void Abandon()
    {
        CallingCq = false;
        _tailEndersUntil = null;
        if (InContact) _outcome = ContactOutcome.Abandoned;
    }

    /// <summary>The keyboard watchdog stopped transmitting.</summary>
    public void Watchdog()
    {
        CallingCq = false;
        if (InContact) _outcome = ContactOutcome.Watchdog;
    }

    /// <summary>What the strip shows, or null when there has been no contact.</summary>
    public ContactView? View()
    {
        if (_partner is null) return null;
        var rx = Received;
        var status = _outcome switch
        {
            ContactOutcome.Logged => $"Logged {_partner}.",
            ContactOutcome.Watchdog => "Stopped by watchdog.",
            ContactOutcome.Abandoned => "Abandoned.",
            _ => NextMacro is { } k ? $"In contact with {_partner}. Enter sends {Label(k)}." : $"Waiting for {_partner}.",
        };
        return new ContactView(_partner, rx.Grid, _flow, _outcome, _reportSent ? ReportToSend : null, rx.Rst, [.. _steps.Select(s => s.ToView())],
            _logged, !_logged && _outcome != ContactOutcome.Abandoned && rx.Rst is not null, status);
    }

    private static string Label(int key) => Macro.Defaults.FirstOrDefault(m => m.Key == key)?.Label ?? $"F{key}";

    // A word is my call, also with a portable suffix or prefix (W7LIT/P).
    private bool IsMe(string word) => SameStation(word, _myCall);

    private static bool SameStation(string? a, string? b) =>
        Callsign.EqualsCall(a, b) || (a is { Length: > 0 } && b is { Length: > 0 } && Callsign.IsValid(Callsign.Normalize(a))
                                      && string.Equals(Callsign.BaseCall(a), Callsign.BaseCall(b), StringComparison.Ordinal));

    // Text with a sender is the partner's when the sender is the partner; text with none only when it is on the
    // partner's offset and names me, so a nearby station's text is not taken for theirs.
    private bool FromPartner(string? sender, bool toMe, int offsetHz) =>
        sender is not null ? SameStation(sender, _partner) : toMe && Math.Abs(offsetHz - _partnerOffset) <= 50;

    private bool CanStart(string? sender, bool toMe, string? addressee, DateTime utc) =>
        sender is not null && toMe && !IsMe(sender) && (addressee is null || IsMe(addressee))
        && (CallingCq || (_tailEndersUntil is { } until && utc <= until && !SameStation(sender, _partner)));

    private void Append(string upper)
    {
        _heardSinceMine.Append(upper).Append(' ');
        if (_heardSinceMine.Length > MaxHeard) _heardSinceMine.Remove(0, _heardSinceMine.Length - MaxHeard);
    }

    private bool ExchangesDone() => _steps.Where(s => s.Label is "Exchange" or "Their exchange").All(s => s.State == StepState.Done);

    private void Begin(string call, int offsetHz, ContactFlow flow)
    {
        _partner = Callsign.Normalize(call);
        _partnerOffset = offsetHz;
        _flow = flow;
        _outcome = ContactOutcome.InProgress;
        _received = Exchange.None;
        _captured.Clear();
        _heardSinceMine.Clear();
        _logged = false;
        _reportSent = false;
        _heardToMe = false;
        _tailEndersUntil = null;
        if (flow == ContactFlow.AnswerCq) _timeOn = null;
    }

    // The first step not done becomes the current one.
    private void Advance()
    {
        var first = true;
        for (var i = 0; i < _steps.Length; i++)
        {
            if (_steps[i].State == StepState.Done) continue;
            _steps[i] = _steps[i] with { State = first ? StepState.Now : StepState.Upcoming };
            first = false;
        }
    }

    private void Log(DateTime utc)
    {
        if (_logged || _partner is null) return;
        _logged = true;
        _outcome = ContactOutcome.Logged;
        _tailEndersUntil = utc + TailEnderWindow;
        var rx = Received;
        // The report is logged as sent only once it went out.
        Logged?.Invoke(new KeyboardLogEntry(new ContactLogEntry(_partner, rx.Grid, _reportSent ? ReportToSend : null, rx.Rst, _timeOn ?? utc, utc),
            rx.Name, rx.Qth, _partnerOffset));
    }

    private static string Shorten(string text)
    {
        var t = string.Join(' ', Words().Split(text).Where(w => w.Length > 0));
        return t.Length <= 60 ? t : t[..60];
    }

    private static string Capitalise(string v) => string.Join(' ', v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => w.Length <= 1 || w.Any(char.IsDigit) ? w : w[0] + w[1..].ToLowerInvariant()));

    [GeneratedRegex(@"[^A-Z0-9/?]+")]
    private static partial Regex Words();

    [GeneratedRegex("^[1-5][1-9][1-9]$")]
    private static partial Regex RstWord();

    private sealed record Step(bool Mine, string Label, string Message, StepState State, DateTime? TimeUtc, int Key, int Count = 0)
    {
        public ContactStep ToView() => new(Mine, Message, State, Count, TimeUtc);
    }
}
