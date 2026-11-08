// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.Core.Contests;
using Ft8Client.Core.Encoding;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// Asks for my exchange before a contest starts, and shows the messages it will send. Start is offered only when every
/// one of them can be sent exactly as shown.
/// </summary>
public sealed partial class ContestExchangeViewModel : ObservableObject
{
    private readonly string _myCall;
    private readonly string _myGrid;

    /// <summary>Creates the sheet for <paramref name="contest"/>.</summary>
    /// <param name="contest">The contest being started.</param>
    /// <param name="myCall">My callsign.</param>
    /// <param name="myGrid">My grid.</param>
    /// <param name="saved">The exchange saved last time, if any.</param>
    public ContestExchangeViewModel(ContestInfo contest, string myCall, string myGrid, string? saved)
    {
        Contest = contest;
        _myCall = myCall.Trim().ToUpperInvariant();
        _myGrid = myGrid.Trim();
        Exchange = saved ?? string.Empty;
        Refresh();
    }

    /// <summary>The contest.</summary>
    public ContestInfo Contest { get; }

    /// <summary>The contest's name, as the title.</summary>
    public string Title => Contest.Name;

    /// <summary>What to type, for the box's label.</summary>
    public string Prompt => Contest.Exchange switch
    {
        ContestExchangeKind.Roundup => "Your state or province (type DX outside the US and Canada to send serial numbers)",
        ContestExchangeKind.FieldDay => "Your class and section, such as 1D UT",
        _ => "Your grid (from Settings) is the exchange",
    };

    /// <summary>True when there is something to type (not a grid contest).</summary>
    public bool AsksForExchange => Contest.Exchange != ContestExchangeKind.Grid;

    /// <summary>Notes about the exchange that are not typed.</summary>
    public string Note => Contest.Exchange switch
    {
        ContestExchangeKind.Roundup =>
            "The report follows their signal, as WSJT-X does (559 here is an example). The ARRL rules count only RTTY contacts in the Roundup; FT8 contacts are logged but may not score.",
        ContestExchangeKind.FieldDay => "Class is how many transmitters, then A to F. Use DX as the section outside ARRL and RAC sections.",
        _ => "Change your grid in Settings if it is wrong.",
    };

    /// <summary>My exchange as typed.</summary>
    [ObservableProperty]
    public partial string Exchange { get; set; } = string.Empty;

    /// <summary>Why the exchange cannot be used, or null.</summary>
    [ObservableProperty]
    public partial string? Problem { get; set; }

    /// <summary>The messages this contest will send, as (when, text).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial IReadOnlyList<ContestPreviewLine> Preview { get; set; } = [];

    /// <summary>True when there are messages to preview.</summary>
    public bool HasPreview => Preview.Count > 0;

    /// <summary>True when the exchange is good and every message encodes exactly.</summary>
    [ObservableProperty]
    public partial bool CanStart { get; set; }

    /// <summary>The exchange to save when the operator starts.</summary>
    public string Result => ContestExchange.Tidy(Exchange);

    partial void OnExchangeChanged(string value) => Refresh();

    private void Refresh()
    {
        Problem = ContestExchange.Problem(Contest, Exchange, _myGrid);
        if (_myCall.Length == 0) Problem ??= "Set your callsign in Settings first.";
        if (Problem is not null)
        {
            Preview = [];
            CanStart = false;
            return;
        }
        // The other station's call is not known yet: the check packs with my own call in its place, and the preview
        // shows a placeholder, so no made-up callsign appears.
        const string them = "‹call›";
        var mine = ContestExchange.Mine(Contest, Exchange, _myGrid, -9, 1);
        var grid = Core.Messages.Grid.Grid4(_myGrid) ?? string.Empty;
        var lines = new List<(string When, string Text)>
        {
            ("Your CQ", $"CQ {Contest.CqModifier} {_myCall} {grid}".TrimEnd()),
            ("Answering a CQ", $"{them} {_myCall} {mine}"),
            ("Your reply when called", $"{them} {_myCall} R {mine}"),
            ("To finish", $"{them} {_myCall} RR73"),
        };
        var bad = lines.FirstOrDefault(l => !SendsExactly(l.Text.Replace(them, _myCall, StringComparison.Ordinal)));
        Problem = bad.Text is null ? null : $"\"{bad.Text}\" cannot be sent exactly as shown in FT8, so this exchange cannot be used.";
        Preview = [.. lines.Select(l => new ContestPreviewLine(l.When, l.Text))];
        CanStart = Problem is null;
    }

    private static bool SendsExactly(string text)
    {
        var packed = MessagePacker.Pack(text);
        return packed.Ok && MessageUnpacker.Unpack(packed.Payload) == MessagePacker.Normalize(text);
    }
}
