// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>
/// What a radio fault does to a contact, and the work the session hands off the slot thread when a transmission starts.
/// </summary>
public class TransmitSafetySessionTests
{
    // Even slots are the DX station's; I send in the odd ones.
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static DateTime Slot(int n) => T0.AddSeconds(15 * n);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(T0.AddSeconds(-1));
        public readonly ScriptDecoder Decoder = new();
        public readonly SimulatedRig Radio;
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;
        public readonly Log Log = new();
        public readonly List<string> Sent = [];

        public Rig()
        {
            var freqs = ReferenceData.LoadFrequencies();
            Radio = new SimulatedRig(Clock);
            Tx = new Transmitter(Radio, new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = 1500 };
            Session = new Session(config, Decoder, new SilentSource(), Tx, Clock, ReferenceData.LoadCountries(), freqs, Log);
            Session.Transmitting += (tx, _) =>
            {
                lock (Sent) Sent.Add(tx.Plan.Message);
            };
            Slots = new SlotClock(Clock, Mode.Ft8);
            Slots.Event += Session.OnSlotEvent;
            Slots.Poll();
        }

        public async Task RunUntilAsync(DateTime end)
        {
            while (Clock.UtcNow < end)
            {
                Clock.Advance(TimeSpan.FromMilliseconds(10));
                Slots.Poll();
                Tx.Poll();
                await Session.DecodeIdle;
            }
        }

        // ZL2RPA calls CQ, I call, it sends my report, and my R-report is on the air in slot 3.
        public async Task ReachMyReportAsync()
        {
            Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
            await RunUntilAsync(Slot(0).AddSeconds(14.8));
            Session.CallStation("ZL2RPA").Should().BeTrue();
            Decoder.Script(Slot(2), "W7LIT ZL2RPA -05");
            await RunUntilAsync(Slot(3).AddSeconds(5));
            Tx.Transmitting.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RadioFault_ReportsExchanged_KeepsTheContactAndLogsALateRr73()
    {
        var r = new Rig();
        await r.ReachMyReportAsync();

        r.Radio.InjectFault("rigctld lost its connection to the radio");
        await r.RunUntilAsync(Slot(3).AddSeconds(6));

        r.Tx.Transmitting.Should().BeFalse();
        r.Radio.Ptt.Should().BeFalse();
        r.Session.Snapshot.Contact.Should().NotBeNull();
        r.Session.Snapshot.Contact!.Outcome.Should().Be(ContactOutcome.InProgress);
        r.Session.Snapshot.Fault.Should().Contain("lost its connection").And.Contain("ZL2RPA is kept");

        // It heard my report before the radio dropped out and signs off: logged, and nothing more goes out.
        var sentBefore = r.Sent.Count;
        r.Decoder.Script(Slot(4), "W7LIT ZL2RPA RR73");
        await r.RunUntilAsync(Slot(5).AddSeconds(14));

        r.Log.Written.Should().ContainSingle().Which.Call.Should().Be("ZL2RPA");
        r.Sent.Should().HaveCount(sentBefore);
    }

    [Fact]
    public async Task RadioFault_BeforeAnyReport_AbandonsTheContact()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.CallStation("ZL2RPA");
        await r.RunUntilAsync(Slot(1).AddSeconds(5));

        r.Radio.InjectFault("USB cable unplugged");
        await r.RunUntilAsync(Slot(1).AddSeconds(6));

        r.Session.Snapshot.Contact?.Outcome.Should().NotBe(ContactOutcome.InProgress);
        r.Session.Snapshot.Fault.Should().Contain("USB cable unplugged").And.NotContain("is kept");
        r.Log.Written.Should().BeEmpty();
    }

    [Fact]
    public async Task Defer_BackgroundQueue_ContactStillLoggedOnceAndListenersToldInOrder()
    {
        var r = new Rig();
        var errors = new List<Exception>();
        using var work = new WorkQueue("TestWork", errors.Add);
        var slotThread = Environment.CurrentManagedThreadId;
        var logThreads = new List<int>();
        r.Log.OnWrite = () => logThreads.Add(Environment.CurrentManagedThreadId);
        r.Session.Defer = work.Post;

        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.CallStation("ZL2RPA");
        r.Decoder.Script(Slot(2), "W7LIT ZL2RPA -05");
        r.Decoder.Script(Slot(4), "W7LIT ZL2RPA RR73");
        await r.RunUntilAsync(Slot(5).AddSeconds(14));
        work.Flush(TimeSpan.FromSeconds(5)).Should().BeTrue();

        errors.Should().BeEmpty();
        lock (r.Sent) r.Sent.Should().Equal("ZL2RPA W7LIT DN40", "ZL2RPA W7LIT R-10", "ZL2RPA W7LIT 73");
        r.Log.Written.Should().ContainSingle();
        logThreads.Should().ContainSingle().Which.Should().NotBe(slotThread, "the log write must not hold up the thread that releases PTT");
        r.Session.Snapshot.LastLogged!.Call.Should().Be("ZL2RPA");
    }

    [Fact]
    public async Task Record_LogWriteThrows_SaysTheContactWasNotSaved()
    {
        var r = new Rig();
        r.Log.OnWrite = () => throw new IOException("database is locked");
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.CallStation("ZL2RPA");
        r.Decoder.Script(Slot(2), "W7LIT ZL2RPA -05");
        r.Decoder.Script(Slot(4), "W7LIT ZL2RPA RR73");
        await r.RunUntilAsync(Slot(5).AddSeconds(2));

        r.Session.Snapshot.Fault.Should().Contain("ZL2RPA").And.Contain("could not be written to the log").And.Contain("database is locked");
    }

    private sealed class Log : ISessionLog
    {
        public List<ContactLogEntry> Written { get; } = [];

        public Action? OnWrite { get; set; }

        public QsoRecord? Write(ContactLogEntry entry, LogContext context)
        {
            OnWrite?.Invoke();
            lock (Written) Written.Add(entry);
            return null;
        }
    }

    private sealed class ScriptDecoder : IDecoder
    {
        private readonly Dictionary<DateTime, string[]> _script = [];

        public void Script(DateTime slot, params string[] lines)
        {
            lock (_script) _script[slot] = lines;
        }

        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
        {
            string[] lines;
            lock (_script) lines = _script.GetValueOrDefault(slot.SlotStartUtc) ?? [];
            var decodes = lines.Select((l, i) => new Decode(slot.SlotStartUtc, -10, 0.1, 800 + 300 * i, l, false)).ToList();
            return Task.FromResult(new DecodeResult(decodes, TimeSpan.FromSeconds(0.1), DecodeStatus.Ok));
        }
    }

    private sealed class SilentSource : ISlotSource
    {
        public void BeginSlot(DateTime slotStartUtc, Mode mode)
        {
        }

        public void CancelSlot()
        {
        }

        public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode) => new(slotStartUtc, mode, new short[16]);
    }
}
