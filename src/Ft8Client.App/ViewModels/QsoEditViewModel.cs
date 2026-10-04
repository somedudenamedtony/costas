// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.Core.Geo;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>Editable fields of a contact.</summary>
public sealed partial class QsoEditViewModel(QsoRecord q) : ObservableObject
{
    /// <summary>Call.</summary>
    [ObservableProperty]
    public partial string Call { get; set; } = q.Call;

    /// <summary>Grid.</summary>
    [ObservableProperty]
    public partial string Grid { get; set; } = q.Gridsquare ?? string.Empty;

    /// <summary>Sent.</summary>
    [ObservableProperty]
    public partial string Sent { get; set; } = q.RstSent ?? string.Empty;

    /// <summary>Received.</summary>
    [ObservableProperty]
    public partial string Received { get; set; } = q.RstRcvd ?? string.Empty;

    /// <summary>Name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = q.Name ?? string.Empty;

    /// <summary>Comment.</summary>
    [ObservableProperty]
    public partial string Comment { get; set; } = q.Comment ?? string.Empty;

    /// <summary>Writes the fields back.</summary>
    public QsoRecord Apply()
    {
        q.Call = Call.Trim().ToUpperInvariant();
        q.Gridsquare = Blank(Grid);
        q.RstSent = Blank(Sent);
        q.RstRcvd = Blank(Received);
        q.Name = Blank(Name);
        q.Comment = Blank(Comment);
        return q;
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
