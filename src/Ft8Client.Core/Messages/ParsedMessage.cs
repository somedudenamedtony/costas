// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Messages;

/// <summary>A decoded message parsed into its parts.</summary>
public sealed record ParsedMessage
{
    /// <summary>What kind of message this is.</summary>
    public required MessageKind Kind { get; init; }

    /// <summary>The original text, upper case, single-spaced.</summary>
    public required string Text { get; init; }

    /// <summary>Addressee. Null for CQ and free text.</summary>
    public CallToken? To { get; init; }

    /// <summary>Sender. Null for free text.</summary>
    public CallToken? From { get; init; }

    /// <summary>The 4 or 6-character grid sent, if any.</summary>
    public string? Grid { get; init; }

    /// <summary>The signal report sent, if any.</summary>
    public int? Report { get; init; }

    /// <summary>The CQ modifier (<c>DX</c>, <c>NA</c>, <c>POTA</c>, <c>145</c>), if any.</summary>
    public string? CqModifier { get; init; }

    /// <summary>For <see cref="MessageKind.DxpeditionMulti"/>: the second station, which receives the report.</summary>
    public CallToken? To2 { get; init; }

    /// <summary>The sender's call when it is known and resolved; otherwise null.</summary>
    public string? SenderCall => From is { Unresolved: false } f ? f.Call : null;

    /// <summary>True when the message is addressed to <paramref name="call"/>.</summary>
    public bool IsAddressedTo(string call) =>
        (To is { Unresolved: false } t && Callsign.EqualsCall(t.Call, call)) ||
        (To2 is { Unresolved: false } t2 && Callsign.EqualsCall(t2.Call, call));

    /// <summary>
    /// Splits a DXpedition message into the two messages it stands for (RR73 to the first call, a report
    /// to the second); returns any other message unchanged.
    /// </summary>
    public IReadOnlyList<ParsedMessage> Expand()
    {
        if (Kind != MessageKind.DxpeditionMulti) return [this];
        return
        [
            new ParsedMessage { Kind = MessageKind.RogerBye, Text = Text, To = To, From = From },
            new ParsedMessage { Kind = MessageKind.Report, Text = Text, To = To2, From = From, Report = Report },
        ];
    }
}
