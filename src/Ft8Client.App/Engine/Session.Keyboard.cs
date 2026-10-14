// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;
using Ft8Client.Core;
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Services;
using Ft8Client.Core.Transmit;
using Ft8Client.Modems;
using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.App.Engine;

/// <summary>
/// PSK and RTTY contacts and transmit (docs/09-keyboard-modes.md sections 7 to 9): macros and typed text go out
/// through <see cref="KeyboardTransmitter"/>, and the contact strip follows the conversation through
/// <see cref="KeyboardContactEngine"/>. Nothing transmits without an operator's keypress or click.
/// </summary>
public sealed partial class Session
{
    private const int MaxConversation = 200;
    private readonly KeyboardContactEngine _kbd;
    private readonly KeyboardTransmitter _keys;
    private readonly List<ConversationLine> _conversation = [];
    private readonly Dictionary<int, string> _macroTexts = [];
    private string? _radioNote;

    /// <summary>The keyboard transmitter, so tests can render its audio on a manual clock.</summary>
    internal KeyboardTransmitter Keys => _keys;

    /// <summary>True while PTT is down or audio is going out (the keyboard reader drops its own signal).</summary>
    public bool IsTransmitting => _tx.Transmitting;

    private bool KeyboardMode => !ModeInfo.IsSlotted(_config.Mode);

    /// <summary>
    /// Sends the macro on function key <paramref name="key"/>, returning to receive when it has gone. Refused, with the
    /// reason in the bottom bar and returned, when a field has no value, a character cannot be sent, or a transmit
    /// guard says no.
    /// </summary>
    public string? SendMacro(int key)
    {
        string? why;
        lock (_gate)
        {
            if (!KeyboardMode) return null;
            why = QueueMacro(key);
            _fault = why;
        }
        Publish();
        return why;
    }

    /// <summary>
    /// Sends typed text: starts an over that stays on the air for more typing (Enter on an empty line or 20 s of
    /// nothing returns to receive), or adds to the over on the air.
    /// </summary>
    public string? SendText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string? why;
        lock (_gate)
        {
            if (!KeyboardMode || text.Length == 0) return null;
            why = Queue(text, null, returnToReceive: false, TxOffset(cq: false));
            _fault = why;
        }
        Publish();
        return why;
    }

    /// <summary>
    /// Enter. On the air: a new line, or on an empty line the return to receive. Receiving: sends what is typed, or the
    /// next step's macro when nothing is.
    /// </summary>
    public string? KeyEnter(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        if (_keys.Active)
        {
            var v = _keys.View;
            var line = (v.Sent + v.Waiting).Split('\n')[^1];
            if (string.IsNullOrWhiteSpace(line) && typed.Length == 0) _keys.ReturnToReceive();
            else _keys.Type('\n');
            Publish();
            return null;
        }
        if (typed.Length > 0) return SendText(typed);
        int? next;
        lock (_gate) next = KeyboardMode ? _kbd.NextMacro : null;
        return next is { } k ? SendMacro(k) : null;
    }

    /// <summary>A key typed while an over is on the air. False when it cannot be sent (or nothing is on the air).</summary>
    public bool TypeKey(char c) => _keys.Type(c);

    /// <summary>Backspace while an over is on the air: takes back the last character not yet sent.</summary>
    public bool BackspaceKey() => _keys.Backspace();

    /// <summary>A note about the radio's settings for this mode (the RTTY power cap, the speech processor), or null.</summary>
    public void SetRadioNote(string? note)
    {
        lock (_gate)
        {
            if (_radioNote == note) return;
            _radioNote = note;
        }
        Publish();
    }

    /// <summary>Sets part of the exchange (or the partner's call) from a word the operator clicked.</summary>
    public void CaptureWord(ExchangeField field, string word)
    {
        lock (_gate)
        {
            if (!KeyboardMode) return;
            _kbd.Capture(field, word);
        }
        Publish();
    }

    // Keyboard-mode Call: answer the station's CQ with F2.
    private bool CallKeyboardStation(string call)
    {
        lock (_gate)
        {
            if (_tracker.Find(call) is not { } s) return false;
            if (_keys.Active)
            {
                _fault = "Still transmitting: press Esc to stop, or wait for the over to end.";
            }
            else
            {
                _conversation.Clear();
                _kbd.Answer(s.Call, s.Grid, s.OffsetHz, s.RecentText, _clock.UtcNow);
                if (s.RecentText is { Length: > 0 } t) AddConversation(s.Call, t, mine: false);
                _fault = QueueMacro(Macro.AnswerKey);
            }
        }
        Publish();
        return true;
    }

    private void ToggleKeyboardCq()
    {
        lock (_gate)
        {
            if (_kbd.CallingCq) _kbd.StopCq();
            else _fault = QueueMacro(Macro.CqKey);
        }
        Publish();
    }

    // Under _gate.
    private string? QueueMacro(int key)
    {
        if (_config.Macros.FirstOrDefault(m => m.Key == key) is not { } macro) return $"F{key} has no macro. Set one in Settings > PSK and RTTY.";
        if (key != Macro.CqKey && _kbd.Partner is null && macro.Text.Contains("{CALL}", StringComparison.OrdinalIgnoreCase))
            return $"{macro.Label} needs a station: click Call on one first.";
        var rx = _kbd.Received;
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["CALL"] = _kbd.Partner,
            ["NAME"] = rx.Name,
            ["QTH"] = rx.Qth,
            ["RST"] = _kbd.ReportToSend.ToString(CultureInfo.InvariantCulture),
            ["MYCALL"] = _config.MyCall,
            ["MYNAME"] = _config.MyName,
            ["MYQTH"] = _config.MyQth,
            ["MYGRID"] = _config.MyGrid,
            ["MYRIG"] = _config.MyRig,
            ["MYANT"] = _config.MyAntenna,
        };
        var text = MacroText.Expand(macro.Text, values, out var missing);
        if (missing.Count > 0) return MissingFields(macro, missing);
        var cq = key == Macro.CqKey;
        var why = Queue(text + " ", key, returnToReceive: true, TxOffset(cq));
        if (why is not null) return why;
        _macroTexts[key] = text;
        if (cq)
        {
            // Listen for answers from the moment the CQ is queued: a quick reply must not be missed.
            if (!_kbd.InContact) _conversation.Clear();
            _kbd.CqSent(_clock.UtcNow);
        }
        return null;
    }

    private static string MissingFields(Macro macro, IReadOnlyList<string> missing)
    {
        var theirs = missing.Where(f => f is "CALL" or "NAME" or "QTH").ToList();
        var where = theirs.Count == missing.Count
            ? "Click the word in the conversation to set it, or remove it from the macro."
            : "Fill it in under Settings > PSK and RTTY, or remove it from the macro.";
        return $"{macro.Label} not sent: {MacroText.Describe(missing)} {(missing.Count == 1 ? "has" : "have")} no value. {where}";
    }

    // Under _gate. Starts or adds to an over.
    private string? Queue(string text, int? key, bool returnToReceive, int offset)
    {
        var mode = _config.Mode;
        // Reverse is for reading a station on the other sideband; Costas itself always sends the standard way round
        // (mark the higher tone in USB), so every other station reads it.
        var rtty = _config.Rtty with { Reverse = false };
        var guard = new TxGuardInput(_config.Band, _dialHz, null, _config.ClockBlockSeconds,
            _services[ServiceNames.Radio].Health == ServiceHealth.Down ? _services[ServiceNames.Radio].Message : null, true);
        var lead = _config.PttLead;
        return _keys.Send(text, key, returnToReceive,
            () => mode == Mode.Rtty
                ? new RttyModulator(rtty, _tx.OutputSampleRate, offset)
                : new PskModulator(mode, _tx.OutputSampleRate, offset),
            s => _tx.StartStream(s, mode, offset, guard, lead));
    }

    // Under _gate. In a contact, the partner's offset; a CQ goes on the offset last clicked, else a clear spot.
    private int TxOffset(bool cq)
    {
        if (!cq && _kbd.InContact) return _kbd.PartnerOffsetHz;
        if (_config.FixedTxOffsetHz is { } f) return f;
        var half = ModeInfo.OccupiedBandwidthHz(_config.Mode) / 2;
        IReadOnlyList<int> busy = [.. _tracker.Stations.Select(s => s.OffsetHz - half)];
        var c = TxOffsetPicker.Choose([busy], _config.Mode, _txOffset - half);
        _txOffset = c.OffsetHz + half;
        _txOffsetClear = c.Clear;
        return _txOffset;
    }

    // Under _gate. Partner text into the strip and the conversation box; while calling CQ, an answer starts a contact.
    private void ApplyKeyboardLine(KeyboardLine l)
    {
        var before = _kbd.Partner;
        _kbd.Heard(l.Text, l.Sender, l.OffsetHz, l.Utc);
        if (_kbd.Partner is not { } partner) return;
        if (before is not null && !Callsign.EqualsCall(before, partner)) _conversation.Clear();
        var fromPartner = l.Sender is not null
            ? Callsign.EqualsCall(l.Sender, partner)
            : Math.Abs(l.OffsetHz - _kbd.PartnerOffsetHz) <= 50;
        if (fromPartner) AddConversation(partner, l.Text, mine: false);
    }

    private void AddConversation(string who, string text, bool mine)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _conversation.Add(new ConversationLine(_clock.UtcNow, who, text, mine));
        if (_conversation.Count > MaxConversation) _conversation.RemoveRange(0, _conversation.Count - MaxConversation);
    }

    private void OnKeyboardOverEnded(KeyboardOver over)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            AddConversation(_config.MyCall, over.Sent, mine: true);
            foreach (var key in over.Macros.Where(k => k != Macro.CqKey))
                _kbd.Sent(key, _macroTexts.GetValueOrDefault(key) ?? string.Empty, now);
            _macroTexts.Clear();
            if (over.Watchdog)
            {
                _kbd.Watchdog();
                _fault = "Stopped by watchdog: an over is limited to 3 minutes, and closes after 20 seconds with nothing to send.";
            }
        }
        Publish();
    }

    // Keyboard modes log the frequency of the signal itself: the dial plus the partner's audio offset.
    private void OnKeyboardLogged(KeyboardLogEntry k)
    {
        long freq;
        lock (_gate) freq = _dialHz + k.OffsetHz;
        Record(k.Entry, freq, k.Name, k.Qth);
    }
}
