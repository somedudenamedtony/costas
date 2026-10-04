// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>The fields of a Status message.</summary>
public sealed record WsjtxStatus
{
    /// <summary>Dial frequency in hertz.</summary>
    public long DialHz { get; init; }

    /// <summary>Mode name (<c>FT8</c>, <c>FT4</c>).</summary>
    public string Mode { get; init; } = "FT8";

    /// <summary>The partner's call, or empty.</summary>
    public string DxCall { get; init; } = string.Empty;

    /// <summary>Report to send, as text, or empty.</summary>
    public string Report { get; init; } = string.Empty;

    /// <summary>Tx is enabled (a contact or CQ is under way).</summary>
    public bool TxEnabled { get; init; }

    /// <summary>On the air now.</summary>
    public bool Transmitting { get; init; }

    /// <summary>The decoder is running.</summary>
    public bool Decoding { get; init; }

    /// <summary>Receive audio offset in hertz.</summary>
    public int RxDf { get; init; }

    /// <summary>Transmit audio offset in hertz.</summary>
    public int TxDf { get; init; }

    /// <summary>My call.</summary>
    public string DeCall { get; init; } = string.Empty;

    /// <summary>My grid.</summary>
    public string DeGrid { get; init; } = string.Empty;

    /// <summary>The partner's grid, or empty.</summary>
    public string DxGrid { get; init; } = string.Empty;

    /// <summary>The Tx watchdog has fired.</summary>
    public bool TxWatchdog { get; init; }

    /// <summary>T/R period in seconds.</summary>
    public int TrPeriodSeconds { get; init; } = 15;

    /// <summary>Station profile name.</summary>
    public string ConfigurationName { get; init; } = string.Empty;

    /// <summary>The message being sent, or empty.</summary>
    public string TxMessage { get; init; } = string.Empty;
}
