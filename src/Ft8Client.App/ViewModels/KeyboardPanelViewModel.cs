// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Keyboard;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// The PSK and RTTY part of the contact card (docs/09-keyboard-modes.md section 10): the conversation box, the
/// type-ahead line, the macro buttons and a line of hints. While receiving, what is typed waits in the line; Enter
/// sends it. While transmitting, every key goes straight to the transmitter.
/// </summary>
public sealed partial class KeyboardPanelViewModel : ObservableObject
{
    private readonly IOperateCommands _commands;
    private readonly List<char> _refusedOnAir = [];
    private IReadOnlyList<ConversationLine> _shown = [];

    /// <summary>Creates the panel.</summary>
    public KeyboardPanelViewModel(IOperateCommands commands) => _commands = commands;

    /// <summary>True in PSK31, PSK63 and RTTY.</summary>
    [ObservableProperty]
    public partial bool Visible { get; set; }

    /// <summary>True in RTTY, for the Reverse switch.</summary>
    [ObservableProperty]
    public partial bool IsRtty { get; set; }

    /// <summary>The overs exchanged with the station in contact.</summary>
    public ObservableCollection<ConversationRowViewModel> Conversation { get; } = [];

    /// <summary>True when there is something in the conversation box.</summary>
    [ObservableProperty]
    public partial bool HasConversation { get; set; }

    /// <summary>The text typed while receiving, waiting for Enter.</summary>
    [ObservableProperty]
    public partial string Typed { get; set; } = string.Empty;

    /// <summary>True while an over is on the air: keys go straight to the transmitter.</summary>
    [ObservableProperty]
    public partial bool OnAir { get; set; }

    /// <summary>On the air: the part of this over already sent (dimmed).</summary>
    [ObservableProperty]
    public partial string SentPart { get; set; } = string.Empty;

    /// <summary>On the air: the part still to go.</summary>
    [ObservableProperty]
    public partial string WaitingPart { get; set; } = string.Empty;

    /// <summary>
    /// The macro refused because a field has no value, in pieces; the view shows those fields in red until they are filled.
    /// Empty when none.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<MacroSegment> BlockedMacro { get; set; } = [];

    /// <summary>True while a refused macro is shown.</summary>
    [ObservableProperty]
    public partial bool HasBlockedMacro { get; set; }

    /// <summary>The macro buttons.</summary>
    public ObservableCollection<MacroButtonViewModel> Macros { get; } = [];

    /// <summary>One line of hints for the keys.</summary>
    [ObservableProperty]
    public partial string Hints { get; set; } = string.Empty;

    /// <summary>
    /// Characters the mode cannot send: those in the typed line while receiving, or the keys refused while on the air.
    /// Empty when none.
    /// </summary>
    [ObservableProperty]
    public partial string KeyNote { get; set; } = string.Empty;

    /// <summary>True while <see cref="KeyNote"/> has something to say.</summary>
    [ObservableProperty]
    public partial bool HasKeyNote { get; set; }

    /// <summary>A note about the radio's settings for this mode, or empty.</summary>
    [ObservableProperty]
    public partial string RadioNote { get; set; } = string.Empty;

    /// <summary>"Typed ahead" or "Sending", for the on-air panel.</summary>
    public string ReadoutLabel => OnAir ? "Sending" : "Typed ahead";

    /// <summary>Characters ready or left.</summary>
    public string ReadoutCount => (OnAir ? WaitingPart.Length : Typed.Length).ToString(CultureInfo.InvariantCulture);

    /// <summary>"characters left" or "characters ready".</summary>
    public string ReadoutUnit => OnAir ? "characters left" : "characters ready";

    /// <summary>The share of this over sent so far.</summary>
    public double ReadoutProgress => OnAir && SentPart.Length + WaitingPart.Length > 0
        ? (double)SentPart.Length / (SentPart.Length + WaitingPart.Length)
        : 0;

    partial void OnTypedChanged(string value)
    {
        Readout();
        UpdateKeyNote();
    }

    partial void OnOnAirChanged(bool value)
    {
        _refusedOnAir.Clear();
        UpdateKeyNote();
    }

    partial void OnIsRttyChanged(bool value) => UpdateKeyNote();

    private string ModeName => IsRtty ? "RTTY" : "PSK";

    private void UpdateKeyNote()
    {
        if (OnAir)
        {
            KeyNote = _refusedOnAir.Count == 0 ? string.Empty
                : $"Not sent: {ModeName} cannot send {KeyboardCharacters.Show(_refusedOnAir)}. {(_refusedOnAir.Count == 1 ? "That key was" : "Those keys were")} left out.";
        }
        else
        {
            var bad = KeyboardCharacters.Unsendable(IsRtty, Typed);
            KeyNote = bad.Count == 0 ? string.Empty
                : $"{ModeName} cannot send {KeyboardCharacters.Show(bad)}. Remove {(bad.Count == 1 ? "it" : "them")} before pressing Enter.";
        }
        HasKeyNote = KeyNote.Length > 0;
    }

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s)
    {
        Visible = !ModeInfo.IsSlotted(s.Mode);
        IsRtty = s.Mode == Mode.Rtty;
        if (!Visible) return;
        if (!ReferenceEquals(_shown, s.Conversation) && !_shown.SequenceEqual(s.Conversation))
        {
            // New overs are added at the end; the rows already shown stay, so a word being aimed at does not move.
            var keep = s.Conversation.Count >= _shown.Count && Conversation.Count == _shown.Count && s.Conversation.Take(_shown.Count).SequenceEqual(_shown);
            if (!keep) Conversation.Clear();
            foreach (var l in s.Conversation.Skip(keep ? _shown.Count : 0))
                Conversation.Add(new ConversationRowViewModel(l.Utc.ToString("HH:mm:ss", CultureInfo.InvariantCulture), l.Who, l.Text, l.Mine));
        }
        _shown = s.Conversation;
        HasConversation = Conversation.Count > 0;
        OnAir = s.KeyboardTx.Active;
        SentPart = s.KeyboardTx.Sent;
        WaitingPart = s.KeyboardTx.Waiting;
        var macros = s.Macros.Select(m => new MacroButtonViewModel(m.Key, m.Label, m.Text, m.Key == s.NextMacro)).ToList();
        if (!macros.SequenceEqual(Macros))
        {
            Macros.Clear();
            foreach (var m in macros) Macros.Add(m);
        }
        RadioNote = s.RadioNote ?? string.Empty;
        var blocked = s.BlockedMacro ?? [];
        if (!blocked.SequenceEqual(BlockedMacro)) BlockedMacro = blocked;
        HasBlockedMacro = blocked.Count > 0;
        var next = s.Macros.FirstOrDefault(m => m.Key == s.NextMacro);
        var keys = KeysText(s.Macros.Select(m => m.Key));
        var macroHint = keys.Length == 0 ? "No macros: add them in Settings > PSK and RTTY" : $"{keys} {(s.Macros.Count == 1 ? "sends its macro" : "send a macro")}";
        Hints = OnAir
            ? "Typing goes out as you type · Backspace takes back what has not gone · Enter on an empty line returns to receive · Esc stops now"
            : next is not null
                ? $"Enter sends F{next.Key} {next.Label} (or what you typed) · {macroHint} · Click a word to use it · Esc stops"
                : $"Type and press Enter to send · {macroHint} · Click a word to use it · Esc stops";
        Readout();
    }

    /// <summary>"F1 to F6", or "F1, F2 and F4" when there are gaps; empty for none.</summary>
    internal static string KeysText(IEnumerable<int> keys)
    {
        var k = keys.Distinct().Order().ToList();
        if (k.Count == 0) return string.Empty;
        if (k.Count == 1) return $"F{k[0]}";
        if (k.Count > 2 && k[^1] - k[0] == k.Count - 1) return $"F{k[0]} to F{k[^1]}";
        return string.Join(", ", k.Take(k.Count - 1).Select(x => $"F{x}")) + $" and F{k[^1]}";
    }

    private void Readout()
    {
        OnPropertyChanged(nameof(ReadoutLabel));
        OnPropertyChanged(nameof(ReadoutCount));
        OnPropertyChanged(nameof(ReadoutUnit));
        OnPropertyChanged(nameof(ReadoutProgress));
    }

    /// <summary>Sends a macro (its button or function key).</summary>
    [RelayCommand]
    public void SendMacro(int key) => _commands.SendMacro(key);

    /// <summary>
    /// Enter: sends what is typed, or the next macro; on the air, a new line or the return to receive. A line that is
    /// refused (a character the mode cannot send, a transmit guard) stays in the box to be edited; the reason is in the
    /// bottom bar.
    /// </summary>
    public void Enter()
    {
        var onAir = OnAir;
        var typed = onAir ? string.Empty : Typed;
        var why = _commands.KeyboardEnter(typed, onAir);
        if (why is null && typed.Length > 0) Typed = string.Empty;
    }

    /// <summary>A key typed while on the air. False when it cannot be sent.</summary>
    public bool Type(char c) => _commands.KeyboardType(c);

    /// <summary>
    /// Keys typed while an over is on the air go straight to the transmitter. A key the mode cannot send is refused and
    /// named in <see cref="KeyNote"/> until the over ends, never dropped silently. A key the transmitter cannot take now (the over is closing
    /// or has just ended) is kept in the typed line, to go with the next Enter.
    /// </summary>
    public void TypeOnAir(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var kept = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (!KeyboardCharacters.CanSend(IsRtty, c))
            {
                var shown = KeyboardCharacters.Normalize(IsRtty, c);
                if (!_refusedOnAir.Contains(shown)) _refusedOnAir.Add(shown);
            }
            else if (kept.Length > 0 || !_commands.KeyboardType(c))
            {
                kept.Append(KeyboardCharacters.Normalize(IsRtty, c));
            }
        }
        if (kept.Length > 0) Typed += kept.ToString();
        UpdateKeyNote();
    }

    /// <summary>
    /// Text typed while the type-ahead line did not have the keyboard: on the air it goes to the transmitter, otherwise
    /// it is added to the end of the line (in capitals for RTTY).
    /// </summary>
    public void TypeAnywhere(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (OnAir) TypeOnAir(text);
        else Typed += new string([.. text.Select(c => KeyboardCharacters.Normalize(IsRtty, c))]);
    }

    /// <summary>Backspace while on the air.</summary>
    public void Backspace() => _commands.KeyboardBackspace();

    /// <summary>
    /// Backspace while the type-ahead line did not have the keyboard: on the air as <see cref="Backspace"/>, otherwise
    /// the line's last character.
    /// </summary>
    public void BackspaceAnywhere()
    {
        if (OnAir) Backspace();
        else if (Typed.Length > 0) Typed = Typed[..^1];
    }

    /// <summary>Uses a clicked word as part of the exchange.</summary>
    public void Capture(ExchangeField field, string word) => _commands.Capture(field, word);

    /// <summary>Captures the word as the partner's name (context menu).</summary>
    [RelayCommand]
    private void CaptureName(string word) => Capture(ExchangeField.Name, word);

    /// <summary>Captures the word as the partner's QTH.</summary>
    [RelayCommand]
    private void CaptureQth(string word) => Capture(ExchangeField.Qth, word);

    /// <summary>Captures the word as the report received.</summary>
    [RelayCommand]
    private void CaptureRst(string word) => Capture(ExchangeField.Rst, word);

    /// <summary>Captures the word as the partner's grid.</summary>
    [RelayCommand]
    private void CaptureGrid(string word) => Capture(ExchangeField.Grid, word);

    /// <summary>Captures the word as the partner's call.</summary>
    [RelayCommand]
    private void CaptureCall(string word) => Capture(ExchangeField.Call, word);
}
