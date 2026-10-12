// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// Mini mode: a small always-on-top strip that mirrors the Operate view. It holds no state of its own; <see cref="Refresh"/>
/// copies what it shows from the Operate view model. Nothing it does transmits without a click.
/// </summary>
public sealed partial class MiniViewModel : ObservableObject
{
    private readonly OperateViewModel _operate;
    private readonly IOperateCommands _commands;
    private readonly Action _halt;

    /// <summary>Creates the view model.</summary>
    /// <param name="operate">The Operate view model it mirrors.</param>
    /// <param name="commands">Where Call goes.</param>
    /// <param name="halt">Halt Tx.</param>
    public MiniViewModel(OperateViewModel operate, IOperateCommands commands, Action halt)
    {
        _operate = operate;
        _commands = commands;
        _halt = halt;
    }

    /// <summary>Raised when the operator asks for the full window back.</summary>
    public event Action? ExpandRequested;

    /// <summary>"20 m FT8".</summary>
    [ObservableProperty]
    public partial string BandMode { get; set; } = string.Empty;

    /// <summary>"Receiving · 9 s" or "Transmitting · 10 s".</summary>
    [ObservableProperty]
    public partial string SlotText { get; set; } = string.Empty;

    /// <summary>True while transmitting.</summary>
    [ObservableProperty]
    public partial bool Transmitting { get; set; }

    /// <summary>Critical while transmitting, otherwise secondary.</summary>
    [ObservableProperty]
    public partial TextKind SlotKind { get; set; } = TextKind.Secondary;

    /// <summary>Caution when someone is calling the operator, otherwise secondary.</summary>
    [ObservableProperty]
    public partial TextKind CountKind { get; set; } = TextKind.Secondary;

    /// <summary>"6 in line", "2 calling you", or empty.</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    /// <summary>True when someone is calling the operator.</summary>
    [ObservableProperty]
    public partial bool HasCallers { get; set; }

    /// <summary>True when a contact is in progress: the strip shows it instead of the top of the line.</summary>
    [ObservableProperty]
    public partial bool InContact { get; set; }

    /// <summary>True outside a contact with somebody at the top of the line.</summary>
    [ObservableProperty]
    public partial bool HasTop { get; set; }

    /// <summary>True outside a contact with nobody to call.</summary>
    [ObservableProperty]
    public partial bool Empty { get; set; } = true;

    /// <summary>The station at the top of the line, or the one in the contact.</summary>
    [ObservableProperty]
    public partial string Call { get; set; } = string.Empty;

    /// <summary>Where the top station is and its report: "Japan · 5,438 mi · -14".</summary>
    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    /// <summary>Why the top station is worth calling: "New country".</summary>
    [ObservableProperty]
    public partial string Why { get; set; } = string.Empty;

    /// <summary>How <see cref="Why"/> is coloured.</summary>
    [ObservableProperty]
    public partial TextKind WhyKind { get; set; }

    /// <summary>Contact progress, one dot per step.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StepViewModel> Steps { get; set; } = [];

    /// <summary>The message on the air now, or the one being waited for.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>Copies the Operate view's state. UI thread, after each snapshot and each tick.</summary>
    /// <param name="band">Band as shown in the band menu ("20 m"), or null before one is chosen.</param>
    /// <param name="mode">Mode name ("FT8").</param>
    public void Refresh(string? band, string mode)
    {
        BandMode = band is null ? mode : $"{band} {mode}";
        Transmitting = _operate.Transmitting;
        SlotText = $"{_operate.SlotLabel} · {_operate.SlotLeft}";
        SlotKind = Transmitting ? TextKind.Critical : TextKind.Secondary;

        var callers = _operate.WaitingRows.Count;
        HasCallers = callers > 0;
        CountKind = HasCallers ? TextKind.Caution : TextKind.Secondary;
        CountText = callers > 0 ? $"{callers} calling you"
            : _operate.Line.Count > 0 ? string.Create(CultureInfo.InvariantCulture, $"{_operate.Line.Count} in line")
            : string.Empty;

        var contact = _operate.Contact is { Active: true } c ? c : null;
        InContact = contact is not null;
        if (contact is not null)
        {
            Call = contact.Call;
            Detail = string.Empty;
            Why = contact.Need;
            WhyKind = contact.NeedKind;
            Steps = contact.Steps;
            var now = contact.Steps.FirstOrDefault(s => s.Look == StepLook.Transmitting)
                ?? contact.Steps.FirstOrDefault(s => s.Look == StepLook.Waiting);
            Message = now?.Message ?? string.Empty;
            HasTop = false;
            Empty = false;
            return;
        }

        Steps = [];
        Message = string.Empty;
        var top = _operate.Line.FirstOrDefault();
        HasTop = top is not null;
        Empty = top is null;
        Call = top?.Call ?? string.Empty;
        Detail = top is null ? string.Empty : $"{top.Where} · {top.Db}";
        Why = top?.Why ?? string.Empty;
        WhyKind = top?.WhyKind ?? TextKind.Normal;
    }

    /// <summary>Calls the station at the top of the line.</summary>
    [RelayCommand]
    private void CallTop()
    {
        if (HasTop && Call.Length > 0) _commands.Call(Call);
    }

    /// <summary>Halt Tx: always available.</summary>
    [RelayCommand]
    private void Halt() => _halt();

    /// <summary>Back to the full window.</summary>
    [RelayCommand]
    private void Expand() => ExpandRequested?.Invoke();
}
