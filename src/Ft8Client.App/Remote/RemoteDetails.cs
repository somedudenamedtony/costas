// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Remote;

/// <summary>What is known about one station: the details flyout of the Operate view, for the station tapped on the phone.</summary>
public sealed record RemoteDetails
{
    /// <summary>Call.</summary>
    public string Call { get; init; } = string.Empty;

    /// <summary>Name from QRZ, or "—".</summary>
    public string Name { get; init; } = "—";

    /// <summary>Place and distance.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>County, or blank.</summary>
    public string County { get; init; } = string.Empty;

    /// <summary>Grid.</summary>
    public string Grid { get; init; } = "—";

    /// <summary>Bearing.</summary>
    public string Bearing { get; init; } = "—";

    /// <summary>Its local time, or blank.</summary>
    public string LocalTime { get; init; } = string.Empty;

    /// <summary>Licence class, or blank.</summary>
    public string LicenseClass { get; init; } = string.Empty;

    /// <summary>How it confirms, or blank.</summary>
    public string Qsl { get; init; } = string.Empty;

    /// <summary>Zones, or blank.</summary>
    public string Zones { get; init; } = string.Empty;

    /// <summary>Most-wanted rank, or blank.</summary>
    public string Rarity { get; init; } = string.Empty;

    /// <summary>"No QRZ details" hint, or blank.</summary>
    public string LookupNote { get; init; } = string.Empty;

    /// <summary>Need tag while it is heard, or blank.</summary>
    public string Need { get; init; } = string.Empty;

    /// <summary>What it is doing now and its signal, or blank when not heard.</summary>
    public string Heard { get; init; } = string.Empty;

    /// <summary>Pile-up headline, or blank.</summary>
    public string Pileup { get; init; } = string.Empty;

    /// <summary>My contacts with it, newest first.</summary>
    public IReadOnlyList<PastContactViewModel> Past { get; init; } = [];

    /// <summary>"Call", "Answer", or blank when it cannot be called now.</summary>
    public string Button { get; init; } = string.Empty;

    /// <summary>The command the button sends: "call" or "answer" (or "answerAfter" during a contact).</summary>
    public string Command { get; init; } = "call";
}
