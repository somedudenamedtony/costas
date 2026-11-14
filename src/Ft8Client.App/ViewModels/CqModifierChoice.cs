// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Contests;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// One choice in the CQ modifier picker beside Call CQ: empty for a plain CQ, a modifier such as DX, or a contest, which
/// changes the exchange as well (owner decision 2026-11-07). A header row names the contest section and cannot be picked.
/// </summary>
/// <param name="Modifier">The modifier as sent, such as <c>DX</c>; empty for a plain CQ.</param>
/// <param name="Contest">The contest this choice runs; <see cref="ContestId.None"/> for a plain modifier.</param>
/// <param name="IsHeader">True for the section heading.</param>
public sealed record CqModifierChoice(string Modifier, ContestId Contest = ContestId.None, bool IsHeader = false)
{
    /// <summary>The modifiers offered without typing one in Settings.</summary>
    public static readonly IReadOnlyList<string> Common = ["", "DX", "NA", "SA", "EU", "AS", "AF", "OC", "POTA"];

    /// <summary>The heading above the contests.</summary>
    public static readonly CqModifierChoice ContestHeader = new("", IsHeader: true);

    /// <summary>The choices in picker order: the modifiers, the heading, then the contests.</summary>
    public static IEnumerable<CqModifierChoice> All() =>
        Common.Select(m => new CqModifierChoice(m))
            .Append(ContestHeader)
            .Concat(ContestInfo.All.Select(c => new CqModifierChoice(c.CqModifier, c.Id)));

    /// <summary>"CQ", "CQ DX", the contest's name, or the heading.</summary>
    public string Label => IsHeader ? "CONTEST (CHANGES THE EXCHANGE)" : ContestInfo.Of(Contest) is { } c ? c.Name : Modifier.Length == 0 ? "CQ" : "CQ " + Modifier;

    /// <summary>For a contest, what its CQ says (<c>CQ RU</c>); otherwise empty.</summary>
    public string Hint => Contest == ContestId.None ? string.Empty : "CQ " + Modifier;

    /// <summary>False for the heading.</summary>
    public bool Selectable => !IsHeader;
}
