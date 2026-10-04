// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>A row of the Log view.</summary>
public sealed class LogRowViewModel(QsoRecord q)
{
    /// <summary>The record.</summary>
    public QsoRecord Record { get; } = q;

    /// <summary>Date.</summary>
    public string Date { get; } = q.QsoDateOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Time.</summary>
    public string Time { get; } = q.QsoDateOn.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Call.</summary>
    public string Call { get; } = q.Call;

    /// <summary>Band.</summary>
    public string Band { get; } = q.Band;

    /// <summary>Mode.</summary>
    public string Mode { get; } = q.Submode ?? q.Mode;

    /// <summary>Sent.</summary>
    public string Sent { get; } = q.RstSent ?? string.Empty;

    /// <summary>Received.</summary>
    public string Received { get; } = q.RstRcvd ?? string.Empty;

    /// <summary>Grid.</summary>
    public string Grid { get; } = q.Gridsquare ?? string.Empty;

    /// <summary>Country.</summary>
    public string Country { get; } = q.Country ?? string.Empty;

    /// <summary>Name.</summary>
    public string Name { get; } = q.Name ?? string.Empty;

    /// <summary>Need tag at the time.</summary>
    public string Need { get; } = q.NeedTier is { } t and >= 1 and <= 5 ? StationText.Need((NeedTag)(t - 1)) : string.Empty;

    /// <summary>QRZ status.</summary>
    public string Qrz { get; } = q.UploadState switch
    {
        UploadState.Uploaded => q.Source == QsoSource.Qrz ? "From QRZ" : "Uploaded",
        UploadState.Queued => "Waiting to upload",
        UploadState.Failed => "Failed: " + q.UploadError,
        _ => q.QrzLogid is null ? "Not uploaded" : "In QRZ",
    };

    /// <summary>QRZ status colour.</summary>
    public TextKind QrzKind { get; } = q.UploadState switch
    {
        UploadState.Queued => TextKind.Caution,
        UploadState.Failed => TextKind.Critical,
        _ => TextKind.Secondary,
    };

    /// <summary>Confirmed.</summary>
    public string Confirmed { get; } = q.Confirmed ? "Yes" : string.Empty;
}
