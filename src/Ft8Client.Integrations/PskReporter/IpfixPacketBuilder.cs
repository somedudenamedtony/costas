// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Buffers.Binary;
using System.Text;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>
/// Builds PSK Reporter IPFIX (RFC 5101) datagrams. Layout ported from WSJT-X Network/PSKReporterIPFIX.cpp
/// (GPLv3, WSJT Development Group): enterprise number 30351; sender template 0x50E3 (senderCallsign, 5-byte frequency,
/// sNR, mode, senderLocator, informationSource, flowStartSeconds); receiver options template 0x50E2 scoped by
/// receiverCallsign (receiverLocator, decodingSoftware, antennaInformation, rigInformation). Strings carry a one-byte
/// length; every set is padded to 4 bytes. See docs/08-open-questions.md, V9.
/// </summary>
public static class IpfixPacketBuilder
{
    /// <summary>Maximum UDP payload (1000-byte IPv6 packet minus headers), as WSJT-X uses.</summary>
    public const int MaxUdpPayload = 1000 - 40 - 8;

    /// <summary>PSK Reporter's IANA private enterprise number.</summary>
    public const uint Pen = 30351;

    /// <summary>Sender records template id.</summary>
    public const ushort SenderTemplateId = 0x50E3;

    /// <summary>Receiver options template id.</summary>
    public const ushort ReceiverTemplateId = 0x50E2;

    /// <summary>Builds the datagrams for a batch of spots.</summary>
    public static IReadOnlyList<IpfixPacket> Build(PskReceiver receiver, IReadOnlyList<PskSpot> spots, bool includeDescriptors,
                                              uint sequence, uint observationId, uint exportTime, int maxPayload = MaxUdpPayload)
    {
        var packets = new List<IpfixPacket>();
        var receiverSet = ReceiverSet(receiver);
        var firstBase = includeDescriptors ? Concat(DescriptorSets(), receiverSet) : receiverSet;
        if (spots.Count == 0)
        {
            packets.Add(new IpfixPacket(Message(firstBase, sequence, observationId, exportTime), 0));
            return packets;
        }

        var records = new List<byte[]>();
        var recordBytes = 0;
        var baseSets = firstBase;
        foreach (var spot in spots)
        {
            var record = SpotRecord(spot);
            var candidate = MessageLength(baseSets.Length + SenderSetLength(recordBytes + record.Length));
            if (records.Count > 0 && candidate > maxPayload)
            {
                packets.Add(new IpfixPacket(Message(Concat(baseSets, SenderSet(records)), sequence, observationId, exportTime), records.Count));
                sequence += (uint)records.Count;
                records.Clear();
                recordBytes = 0;
                baseSets = receiverSet; // follow-on packets keep receiver data, not descriptors
            }
            records.Add(record);
            recordBytes += record.Length;
        }
        if (records.Count > 0) packets.Add(new IpfixPacket(Message(Concat(baseSets, SenderSet(records)), sequence, observationId, exportTime), records.Count));
        return packets;
    }

    /// <summary>The two template sets.</summary>
    public static byte[] DescriptorSets()
    {
        var w = new Writer();
        // Sender template set (set id 2).
        w.U16(2).U16(0).U16(SenderTemplateId).U16(7);
        w.U16(0x8000 + 1).U16(0xFFFF).U32(Pen);  // senderCallsign
        w.U16(0x8000 + 5).U16(5).U32(Pen);       // frequency, 5 bytes
        w.U16(0x8000 + 6).U16(1).U32(Pen);       // sNR
        w.U16(0x8000 + 10).U16(0xFFFF).U32(Pen); // mode
        w.U16(0x8000 + 3).U16(0xFFFF).U32(Pen);  // senderLocator
        w.U16(0x8000 + 11).U16(1).U32(Pen);      // informationSource
        w.U16(150).U16(4);                       // flowStartSeconds
        var sender = w.FinishSet();
        // Receiver options template set (set id 3), scope field count 1.
        w = new Writer();
        w.U16(3).U16(0).U16(ReceiverTemplateId).U16(5).U16(1);
        w.U16(0x8000 + 2).U16(0xFFFF).U32(Pen);  // receiverCallsign (scope)
        w.U16(0x8000 + 4).U16(0xFFFF).U32(Pen);  // receiverLocator
        w.U16(0x8000 + 8).U16(0xFFFF).U32(Pen);  // decodingSoftware
        w.U16(0x8000 + 9).U16(0xFFFF).U32(Pen);  // antennaInformation
        w.U16(0x8000 + 13).U16(0xFFFF).U32(Pen); // rigInformation
        var recv = w.FinishSet();
        return Concat(sender, recv);
    }

    private static byte[] ReceiverSet(PskReceiver r)
    {
        var w = new Writer();
        w.U16(ReceiverTemplateId).U16(0);
        w.Str(r.Callsign, 32).Str(r.Locator, 16).Str(r.Software, 80).Str(r.Antenna, 128).Str(r.Rig, 128);
        return w.FinishSet();
    }

    private static byte[] SpotRecord(PskSpot s)
    {
        var w = new Writer();
        w.Str(s.Callsign, 32);
        w.U8((byte)((s.FrequencyHz >> 32) & 0xFF)).U32((uint)(s.FrequencyHz & 0xFFFFFFFF));
        w.U8((byte)(sbyte)Math.Clamp(s.Snr, sbyte.MinValue, sbyte.MaxValue));
        w.Str(s.Mode, 16).Str(s.Locator, 16);
        w.U8(1); // informationSource: automatically extracted
        w.U32((uint)new DateTimeOffset(DateTime.SpecifyKind(s.TimeUtc, DateTimeKind.Utc)).ToUnixTimeSeconds());
        return w.ToArray();
    }

    private static byte[] SenderSet(List<byte[]> records)
    {
        var w = new Writer();
        w.U16(SenderTemplateId).U16(0);
        foreach (var r in records) w.Raw(r);
        return w.FinishSet();
    }

    private static byte[] Message(byte[] sets, uint sequence, uint observationId, uint exportTime)
    {
        var w = new Writer();
        w.U16(10).U16(0).U32(exportTime).U32(sequence).U32(observationId).Raw(sets);
        return w.FinishSet();
    }

    private static int Pad(int len) => (4 - len % 4) % 4;

    private static int SenderSetLength(int recordBytes)
    {
        var len = 4 + recordBytes;
        return len + Pad(len);
    }

    private static int MessageLength(int setBytes)
    {
        var len = 16 + setBytes;
        return len + Pad(len);
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        a.CopyTo(r, 0);
        b.CopyTo(r, a.Length);
        return r;
    }

    private sealed class Writer
    {
        private readonly MemoryStream _ms = new();

        public Writer U8(byte v)
        {
            _ms.WriteByte(v);
            return this;
        }

        public Writer U16(int v)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)v);
            _ms.Write(b);
            return this;
        }

        public Writer U32(uint v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(b, v);
            _ms.Write(b);
            return this;
        }

        public Writer Str(string s, int max)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            var n = Math.Min(bytes.Length, max);
            while (n > 0 && n < bytes.Length && (bytes[n] & 0xC0) == 0x80) n--;
            _ms.WriteByte((byte)n);
            _ms.Write(bytes, 0, n);
            return this;
        }

        public Writer Raw(byte[] b)
        {
            _ms.Write(b);
            return this;
        }

        public byte[] ToArray() => _ms.ToArray();

        // Pads to 4 bytes and writes the length into bytes 2-3, as setLength() does in WSJT-X.
        public byte[] FinishSet()
        {
            var pad = Pad((int)_ms.Length);
            for (var i = 0; i < pad; i++) _ms.WriteByte(0);
            var a = _ms.ToArray();
            BinaryPrimitives.WriteUInt16BigEndian(a.AsSpan(2, 2), (ushort)a.Length);
            return a;
        }
    }
}
