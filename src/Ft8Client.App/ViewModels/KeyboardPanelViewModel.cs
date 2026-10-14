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

    /// <summary>The macro buttons.</summary>
    public ObservableCollection<MacroButtonViewModel> Macros { get; } = [];

    /// <summary>One line of hints for the keys.</summary>
    [ObservableProperty]
    public partial string Hints { get; set; } = string.Empty;

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

    partial void OnTypedChanged(string value) => Readout();

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s)
    {
        Visible = !ModeInfo.IsSlotted(s.Mode);
        IsRtty = s.Mode == Mode.Rtty;
        if (!Visible) return;
        if (!ReferenceEquals(_shown, s.Conversation) && !_shown.SequenceEqual(s.Conversation))
        {
            Conversation.Clear();
            foreach (var l in s.Conversation)
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
        var next = s.Macros.FirstOrDefault(m => m.Key == s.NextMacro);
        Hints = OnAir
            ? "Typing goes out as you type · Backspace takes back what has not gone · Enter on an empty line returns to receive · Esc stops now"
            : next is not null
                ? $"Enter sends F{next.Key} {next.Label} (or what you typed) · F1 to F{Math.Max(5, s.Macros.Count)} send a macro · Click a word to use it · Esc stops"
                : "Type and press Enter to send · F1 to F5 send a macro · Click a word to use it · Esc stops";
        Readout();
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

    /// <summary>Enter: sends what is typed, or the next macro; on the air, a new line or the return to receive.</summary>
    public void Enter()
    {
        var typed = OnAir ? string.Empty : Typed;
        _commands.KeyboardEnter(typed);
        if (!OnAir) Typed = string.Empty;
    }

    /// <summary>A key typed while on the air. False when it cannot be sent.</summary>
    public bool Type(char c) => _commands.KeyboardType(c);

    /// <summary>Backspace while on the air.</summary>
    public void Backspace() => _commands.KeyboardBackspace();

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
