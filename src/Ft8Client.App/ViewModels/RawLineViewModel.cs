// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Engine;

namespace Ft8Client.App.ViewModels;

/// <summary>One line of Raw decodes (or a blank separator between slots).</summary>
public sealed class RawLineViewModel
{
    /// <summary>A separator.</summary>
    public static RawLineViewModel Blank { get; } = new();

    private RawLineViewModel()
    {
    }

    /// <summary>Builds a line.</summary>
    public RawLineViewModel(RawDecodeLine l)
    {
        var d = l.Decode;
        Utc = d.SlotStartUtc.ToString("HHmmss", CultureInfo.InvariantCulture);
        Db = l.Transmitted ? "Tx" : d.Snr.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        Dt = l.Transmitted ? string.Empty : d.Dt.ToString("0.0", CultureInfo.InvariantCulture);
        Hz = d.OffsetHz.ToString(CultureInfo.InvariantCulture);
        Message = d.Text + (d.LowConfidence ? " ?" : string.Empty) + (d.APriori > 0 ? $" a{d.APriori}" : string.Empty);
        Country = l.Country ?? string.Empty;
        Kind = l.Transmitted ? TextKind.Secondary : l.ToMe ? TextKind.Critical : l.IsCq ? TextKind.Accent : TextKind.Normal;
        Sender = l.Message.SenderCall;
    }

    /// <summary>Slot time.</summary>
    public string Utc { get; } = string.Empty;

    /// <summary>SNR.</summary>
    public string Db { get; } = string.Empty;

    /// <summary>DT.</summary>
    public string Dt { get; } = string.Empty;

    /// <summary>Offset.</summary>
    public string Hz { get; } = string.Empty;

    /// <summary>Message.</summary>
    public string Message { get; } = string.Empty;

    /// <summary>Country.</summary>
    public string Country { get; } = string.Empty;

    /// <summary>Colour: to me critical, CQ accent.</summary>
    public TextKind Kind { get; }

    /// <summary>Sender, for double-click.</summary>
    public string? Sender { get; }

    /// <summary>True for a separator line.</summary>
    public bool IsBlank => ReferenceEquals(this, Blank);
}
