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
using Ft8Client.App.Engine;

namespace Ft8Client.App.ViewModels;

/// <summary>The amber bar across Operate while a contest runs: what I send, the count so far, and the way out.</summary>
public sealed partial class ContestBarViewModel : ObservableObject
{
    private readonly IOperateCommands _commands;

    /// <summary>Creates the bar.</summary>
    public ContestBarViewModel(IOperateCommands commands) => _commands = commands;

    /// <summary>True while a contest runs.</summary>
    [ObservableProperty]
    public partial bool Visible { get; set; }

    /// <summary>The contest's name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>My exchange as it goes out, such as <c>5x9 UT</c>.</summary>
    [ObservableProperty]
    public partial string Sending { get; set; } = string.Empty;

    /// <summary>Contacts that count (dupes left out).</summary>
    [ObservableProperty]
    public partial string Contacts { get; set; } = "0";

    /// <summary>Multipliers so far.</summary>
    [ObservableProperty]
    public partial string Mults { get; set; } = "0";

    /// <summary>True when the contest has multipliers.</summary>
    [ObservableProperty]
    public partial bool HasMults { get; set; }

    /// <summary>Dupes worked anyway.</summary>
    [ObservableProperty]
    public partial string Dupes { get; set; } = "0";

    /// <summary>Why my exchange cannot be sent, or empty.</summary>
    [ObservableProperty]
    public partial string Problem { get; set; } = string.Empty;

    /// <summary>Shows the contest in <paramref name="c"/>, or hides the bar.</summary>
    public void Apply(ContestSnapshot? c)
    {
        Visible = c is not null;
        if (c is null) return;
        Name = c.Info.Name;
        Sending = c.Sending;
        Contacts = c.Score.Contacts.ToString(CultureInfo.InvariantCulture);
        Mults = c.Score.Mults.Count.ToString(CultureInfo.InvariantCulture);
        HasMults = c.Score.HasMults;
        Dupes = c.Score.Dupes.ToString(CultureInfo.InvariantCulture);
        Problem = c.Problem ?? string.Empty;
    }

    /// <summary>Opens the exchange sheet.</summary>
    [RelayCommand]
    private void ChangeExchange() => _commands.ChangeContestExchange();

    /// <summary>Ends the contest: CQ and the exchange go back to normal. The contest log stays in the log.</summary>
    [RelayCommand]
    private void End() => _commands.SetContest(Core.Contests.ContestId.None);
}
