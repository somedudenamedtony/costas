// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using CommunityToolkit.Mvvm.ComponentModel;

namespace Ft8Client.App.ViewModels;

/// <summary>A station profile as edited in Settings, applied on Save.</summary>
public sealed partial class ProfileDraft : ObservableObject
{
    /// <summary>Stable id.</summary>
    public required string Id { get; init; }

    /// <summary>For a profile added in this Settings window, the id of the profile it copies (radio and audio); else null.</summary>
    public string? CopyOf { get; init; }

    /// <summary>Display name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Callsign as typed.</summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>Grid as typed.</summary>
    public string Grid { get; set; } = string.Empty;

    /// <summary>Power in watts as typed.</summary>
    public string Power { get; set; } = string.Empty;

    /// <summary>QRZ logbook API key as typed (kept in the credential store per profile).</summary>
    public string LogbookKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Name;
}
