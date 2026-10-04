// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>The fields of a QSO Logged message.</summary>
public sealed record WsjtxQso
{
    /// <summary>Time off.</summary>
    public DateTime TimeOffUtc { get; init; }

    /// <summary>Time on.</summary>
    public DateTime TimeOnUtc { get; init; }

    /// <summary>Partner's call.</summary>
    public string DxCall { get; init; } = string.Empty;

    /// <summary>Partner's grid, or empty.</summary>
    public string DxGrid { get; init; } = string.Empty;

    /// <summary>Transmit (dial) frequency in hertz.</summary>
    public long FrequencyHz { get; init; }

    /// <summary>Mode name.</summary>
    public string Mode { get; init; } = "FT8";

    /// <summary>Report sent.</summary>
    public string ReportSent { get; init; } = string.Empty;

    /// <summary>Report received.</summary>
    public string ReportReceived { get; init; } = string.Empty;

    /// <summary>Tx power.</summary>
    public string TxPower { get; init; } = string.Empty;

    /// <summary>Comments.</summary>
    public string Comments { get; init; } = string.Empty;

    /// <summary>Partner's name, or empty.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Operator call (empty when the same as my call).</summary>
    public string OperatorCall { get; init; } = string.Empty;

    /// <summary>My call.</summary>
    public string MyCall { get; init; } = string.Empty;

    /// <summary>My grid.</summary>
    public string MyGrid { get; init; } = string.Empty;
}
