// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>Builds WSJT-X UDP datagrams (schema 2, as WSJT-X sends before negotiation).</summary>
public static class WsjtxMessages
{
    /// <summary>The datagram magic number.</summary>
    public const uint Magic = 0xADBCCBDA;

    /// <summary>The schema written in every header.</summary>
    public const uint Schema = 2;

    /// <summary>The highest schema advertised in the heartbeat.</summary>
    public const uint MaxSchema = 3;

    /// <summary>WSJT-X's mode character for a mode name (<c>~</c> FT8, <c>+</c> FT4).</summary>
    public static string ModeChar(string mode) => mode.Equals("FT4", StringComparison.OrdinalIgnoreCase) ? "+" : "~";

    /// <summary>Heartbeat.</summary>
    public static byte[] Heartbeat(string id, string version, string revision) =>
        Header(WsjtxMessageType.Heartbeat, id).UInt32(MaxSchema).Utf8(version).Utf8(revision).ToArray();

    /// <summary>Status.</summary>
    public static byte[] Status(string id, WsjtxStatus s) => Header(WsjtxMessageType.Status, id)
        .UInt64((ulong)Math.Max(0, s.DialHz))
        .Utf8(s.Mode).Utf8(s.DxCall).Utf8(s.Report).Utf8(s.Mode)
        .Bool(s.TxEnabled).Bool(s.Transmitting).Bool(s.Decoding)
        .UInt32((uint)Math.Max(0, s.RxDf)).UInt32((uint)Math.Max(0, s.TxDf))
        .Utf8(s.DeCall).Utf8(s.DeGrid).Utf8(s.DxGrid)
        .Bool(s.TxWatchdog)
        .Utf8(string.Empty) // sub-mode
        .Bool(false) // fast mode
        .UInt8(0) // special operation: none
        .UInt32(uint.MaxValue) // frequency tolerance: not applicable
        .UInt32((uint)s.TrPeriodSeconds)
        .Utf8(s.ConfigurationName)
        .Utf8(s.TxMessage)
        .ToArray();

    /// <summary>Decode.</summary>
    public static byte[] Decode(string id, WsjtxDecode d) => Header(WsjtxMessageType.Decode, id)
        .Bool(true)
        .Time(d.TimeUtc)
        .Int32(d.Snr)
        .Double(d.Dt)
        .UInt32((uint)Math.Max(0, d.OffsetHz))
        .Utf8(ModeChar(d.Mode))
        .Utf8(d.Message)
        .Bool(d.LowConfidence)
        .Bool(d.OffAir)
        .ToArray();

    /// <summary>Clear (outbound form, no window field).</summary>
    public static byte[] Clear(string id) => Header(WsjtxMessageType.Clear, id).ToArray();

    /// <summary>QSO Logged.</summary>
    public static byte[] QsoLogged(string id, WsjtxQso q) => Header(WsjtxMessageType.QsoLogged, id)
        .DateTimeUtc(q.TimeOffUtc)
        .Utf8(q.DxCall).Utf8(q.DxGrid)
        .UInt64((ulong)Math.Max(0, q.FrequencyHz))
        .Utf8(q.Mode).Utf8(q.ReportSent).Utf8(q.ReportReceived).Utf8(q.TxPower).Utf8(q.Comments).Utf8(q.Name)
        .DateTimeUtc(q.TimeOnUtc)
        .Utf8(q.OperatorCall).Utf8(q.MyCall).Utf8(q.MyGrid)
        .Utf8(string.Empty).Utf8(string.Empty) // exchange sent, received
        .Utf8(string.Empty) // ADIF propagation mode
        .ToArray();

    /// <summary>Close.</summary>
    public static byte[] Close(string id) => Header(WsjtxMessageType.Close, id).ToArray();

    /// <summary>Logged ADIF: an ADIF header and one record.</summary>
    public static byte[] LoggedAdif(string id, string adif) => Header(WsjtxMessageType.LoggedAdif, id).Utf8(adif).ToArray();

    private static QDataStreamWriter Header(WsjtxMessageType type, string id) =>
        new QDataStreamWriter().UInt32(Magic).UInt32(Schema).UInt32((uint)type).Utf8(id);
}
