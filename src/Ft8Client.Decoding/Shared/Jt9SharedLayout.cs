// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Messages;

namespace Ft8Client.Decoding.Shared;

/// <summary>
/// Layout of WSJT-X's <c>dec_data</c> (<c>commons.h</c>, <c>lib/jt9com.f90</c>), the block <c>jt9 -s</c> reads from
/// shared memory. Offsets were taken from the C header of WSJT-X 2.7.0 and 3.0.2 compiled with gcc on x86-64. 3.0
/// appends the multithreaded decoder's fields to the 2.7 parameter block, so one segment of the 3.0 size serves both.
/// </summary>
internal static class Jt9SharedLayout
{
    /// <summary><c>ipc[1]</c>: 1 asks <c>jt9</c> to decode, 999 to exit. <c>jt9</c> sets it back to 0 when it starts.</summary>
    public const int IpcStart = 4;

    /// <summary><c>ipc[2]</c>: 1 tells <c>jt9</c> its output was read, so it can take the next request.</summary>
    public const int IpcDone = 8;

    /// <summary>The audio: 16-bit samples at 12 kHz (<c>d2</c>, <c>id2</c> in Fortran).</summary>
    public const int Audio = 5_075_032;

    /// <summary>Start of <c>params</c>.</summary>
    public const int Params = 48_275_032;

    /// <summary>Size of <c>dec_data</c> in WSJT-X 3.0.</summary>
    public const int Size = 48_275_376;

    /// <summary>Samples per half-symbol step; <c>nzhsym</c> counts these.</summary>
    public const int StepSamples = 3456;

    /// <summary><c>nzhsym</c> of the early pass (11.8 s).</summary>
    public const int EarlyStep = 41;

    /// <summary><c>nzhsym</c> of the second early pass (13.5 s), which only subtracts what the first found.</summary>
    public const int SecondStep = 47;

    /// <summary><c>nzhsym</c> of the full FT8 decode.</summary>
    public const int FullStep = 50;

    /// <summary>Samples cleared and written per request: one FT8 slot.</summary>
    public const int SlotSamples = 180_000;

    private const int Nutc = 0, NDiskDat = 4, NtrPeriod = 8, NQsoProgress = 12, Nfqso = 16, Nftx = 20, NewDat = 24, Npts8 = 28,
        Nfa = 32, NfSplit = 36, Nfb = 40, Ntol = 44, Kin = 48, Nzhsym = 52, NSubmode = 56, NAgain = 60, NDepth = 64, LFt8ApOn = 68,
        LApCqOnly = 69, LJt65ApOn = 70, NApWid = 72, NTxMode = 76, NMode = 80, MinW = 84, NClearAve = 88, MinSync = 92,
        EmeDelay = 96, DtTol = 100, NList = 104, N2Pass = 148, NRanEra = 152, NAggressive = 156, NRobust = 160,
        NExpDecode = 164, MaxDrift = 168, DateTime = 172, MyCall = 192, MyGrid = 204, HisCall = 210, HisGrid = 222,
        BEvenSeq = 228, BSuperFox = 229, YyMmDd = 232;

    // WSJT-X 3.0 only (ignored by 2.7).
    private const int MyBCall = 236, HisBCall = 248, NCandThin = 260, NDtCenter = 264, NFt8Cycles = 268, NPrePass = 288,
        NsDecAtt = 292, Nft8RxfSens = 308, Nft4Depth = 312, NStopHint = 321, FMaskAct = 323, LMultiFt8 = 324, LFt8LowTh = 325,
        LCommonFt8b = 330, LMyCallStd = 331, LHisCallStd = 332, NDecoderStart = 340;

    /// <summary>
    /// Writes one request: the parameter block and the audio. <paramref name="step"/> is <c>nzhsym</c>; with
    /// <paramref name="fromDisk"/> set, <c>jt9</c> runs its own early passes over the whole slot, as it does for a file.
    /// Only the first <paramref name="samples"/> samples of the slot are passed; the rest is silence.
    /// </summary>
    public static void WriteRequest(SharedMemorySegment m, SlotAudio slot, DecodeContext ctx, int step, bool fromDisk, int samples)
    {
        var p = Params;
        var t = slot.SlotStartUtc;
        var nutc = t.Hour * 10000 + t.Minute * 100 + t.Second;
        var ft8 = slot.Mode == Mode.Ft8;
        var superFox = ft8 && ctx.Hound && ctx.SuperFox;

        m.Clear(p, Size - p);
        m.WriteInt32(p + Nutc, nutc);
        m.WriteBool(p + NDiskDat, fromDisk);
        m.WriteInt32(p + NtrPeriod, ft8 ? 15 : 7);
        m.WriteInt32(p + NQsoProgress, ctx.QsoProgress);
        m.WriteInt32(p + Nfqso, superFox ? 750 : ctx.RxOffsetHz);
        m.WriteInt32(p + Nftx, ctx.RxOffsetHz);
        m.WriteBool(p + NewDat, true);
        m.WriteInt32(p + Npts8, step * StepSamples / 16);
        m.WriteInt32(p + Nfa, ctx.LowHz);
        m.WriteInt32(p + NfSplit, 2700);
        m.WriteInt32(p + Nfb, ctx.HighHz);
        m.WriteInt32(p + Ntol, superFox ? 100 : 50);
        m.WriteInt32(p + Kin, ModeInfo.DecoderSamples(slot.Mode));
        m.WriteInt32(p + Nzhsym, step);
        m.WriteBool(p + NAgain, false);
        m.WriteInt32(p + NDepth, ctx.Depth);
        m.WriteBool(p + LFt8ApOn, true);
        m.WriteBool(p + LApCqOnly, false);
        m.WriteBool(p + LJt65ApOn, true);
        m.WriteInt32(p + NApWid, 50);
        m.WriteInt32(p + NTxMode, ft8 ? 8 : 5);
        m.WriteInt32(p + NMode, ft8 ? 8 : 5);
        m.WriteInt32(p + MinW, 0);
        m.WriteBool(p + NClearAve, false);
        m.WriteInt32(p + MinSync, 0);
        m.WriteSingle(p + EmeDelay, 0f);
        m.WriteSingle(p + DtTol, 3f);
        m.WriteInt32(p + NList, 0);
        m.WriteInt32(p + N2Pass, 2);
        m.WriteInt32(p + NRanEra, 6);
        m.WriteInt32(p + NAggressive, 0);
        m.WriteBool(p + NRobust, false);
        m.WriteInt32(p + NExpDecode, ctx.Hound && ft8 ? 7 : 0);
        m.WriteInt32(p + MaxDrift, 0);
        m.WriteText(p + DateTime, t.ToString("yyyy-MMM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), 20);
        m.WriteText(p + MyCall, ctx.MyCall, 12);
        m.WriteText(p + MyGrid, ctx.MyGrid, 6);
        m.WriteText(p + HisCall, ctx.DxCall ?? string.Empty, 12);
        m.WriteText(p + HisGrid, ctx.DxGrid ?? string.Empty, 6);
        m.WriteBool(p + BEvenSeq, nutc % 10 == 0);
        m.WriteBool(p + BSuperFox, superFox);
        m.WriteInt32(p + YyMmDd, (t.Year % 100) * 10000 + t.Month * 100 + t.Day);

        m.WriteText(p + MyBCall, ctx.MyCall.Length > 0 ? Callsign.BaseCall(ctx.MyCall) : string.Empty, 12);
        m.WriteText(p + HisBCall, ctx.DxCall is { Length: >= 3 } dx ? Callsign.BaseCall(dx) : string.Empty, 12);
        m.WriteInt32(p + NCandThin, 100);
        m.WriteInt32(p + NDtCenter, 0);
        m.WriteInt32(p + NFt8Cycles, 3);
        m.WriteInt32(p + NPrePass, 4);
        m.WriteInt32(p + NsDecAtt, 1);
        m.WriteInt32(p + Nft8RxfSens, 3);
        m.WriteInt32(p + Nft4Depth, ctx.Depth);
        m.WriteBool(p + NStopHint, false);
        m.WriteBool(p + FMaskAct, true);
        m.WriteBool(p + LMultiFt8, false);
        m.WriteBool(p + LFt8LowTh, true);
        m.WriteBool(p + LCommonFt8b, true);
        m.WriteBool(p + LMyCallStd, ctx.MyCall.Length == 0 || Ft8Client.Core.Encoding.MessagePacker.IsStandardCall(ctx.MyCall));
        m.WriteBool(p + LHisCallStd, ctx.DxCall is not { Length: > 0 } || Ft8Client.Core.Encoding.MessagePacker.IsStandardCall(ctx.DxCall));
        m.WriteInt32(p + NDecoderStart, 0);

        m.WriteSamples(Audio, slot.Samples12k, samples, SlotSamples);
    }
}
