// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Time;

namespace Ft8Client.Audio.Tests;

public class SlotClockTests
{
    private static (SlotClock Clock, ManualClock Time, List<SlotEvent> Events) Make(DateTime start, Mode mode = Mode.Ft8)
    {
        var time = new ManualClock(start);
        var clock = new SlotClock(time, mode);
        var events = new List<SlotEvent>();
        clock.Event += events.Add;
        clock.Poll();
        return (clock, time, events);
    }

    private static void Run(SlotClock c, ManualClock t, TimeSpan duration, int stepMs = 5)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += TimeSpan.FromMilliseconds(stepMs))
        {
            t.Advance(TimeSpan.FromMilliseconds(stepMs));
            c.Poll();
        }
    }

    private static DateTime T(int h, int m, double s) => new DateTime(2026, 1, 4, h, m, 0, DateTimeKind.Utc).AddSeconds(s);

    [Fact]
    public void Poll_JoinMidSlot_SkipsPartialSlotThenAnnouncesBoundariesAndMarks()
    {
        var (c, t, ev) = Make(T(2, 53, 7.3));
        Run(c, t, TimeSpan.FromSeconds(30));
        ev.Where(e => e.Mark == SlotMark.Start).Select(e => e.SlotStartUtc).Should().Equal(T(2, 53, 15), T(2, 53, 30));
        ev.Take(5).Select(e => e.Mark).Should().Equal(SlotMark.Start, SlotMark.PttOn, SlotMark.TxStart, SlotMark.EarlyDecode, SlotMark.CaptureCutoff);
        ev.Should().OnlyContain(e => e.LateBy < TimeSpan.FromMilliseconds(10));
        var ptt = ev.First(e => e.Mark == SlotMark.PttOn);
        c.OffsetOf(SlotMark.PttOn, Mode.Ft8).Should().Be(TimeSpan.FromMilliseconds(300));
        c.OffsetOf(SlotMark.EarlyDecode, Mode.Ft8).Should().Be(TimeSpan.FromSeconds(11.85), "41 steps of 3456 samples, as WSJT-X");
        c.OffsetOf(SlotMark.CaptureCutoff, Mode.Ft8).Should().Be(TimeSpan.FromSeconds(13.6));
        c.OffsetOf(SlotMark.EarlyDecode, Mode.Ft4).Should().Be(c.OffsetOf(SlotMark.CaptureCutoff, Mode.Ft4), "FT4 has no early pass");
        ptt.SlotStartUtc.Should().Be(T(2, 53, 15));
    }

    [Fact]
    public void Poll_SystemTimeJumpsBackMidSlot_NoDoubleSlot()
    {
        var (c, t, ev) = Make(T(2, 53, 14.9));
        Run(c, t, TimeSpan.FromSeconds(5)); // into the 02:53:15 slot
        t.JumpWallClock(TimeSpan.FromSeconds(-8));
        Run(c, t, TimeSpan.FromSeconds(40));
        var starts = ev.Where(e => e.Mark == SlotMark.Start).Select(e => e.SlotStartUtc).ToList();
        starts.Should().OnlyHaveUniqueItems();
        starts.Should().BeInAscendingOrder();
        starts[0].Should().Be(T(2, 53, 15));
        ev.Count(e => e.Mark == SlotMark.CaptureCutoff && e.SlotStartUtc == T(2, 53, 15)).Should().Be(1);
    }

    [Fact]
    public void Poll_SystemTimeJumpsForwardMidSlot_FinishesSlotThenResyncs()
    {
        var (c, t, ev) = Make(T(2, 53, 14.9));
        Run(c, t, TimeSpan.FromSeconds(3));
        t.JumpWallClock(TimeSpan.FromSeconds(1.0));
        Run(c, t, TimeSpan.FromSeconds(30));
        var starts = ev.Where(e => e.Mark == SlotMark.Start).Select(e => e.SlotStartUtc).ToList();
        starts.Should().Equal(T(2, 53, 15), T(2, 53, 30), T(2, 53, 45));
        ev.Count(e => e.Mark == SlotMark.CaptureCutoff && e.SlotStartUtc == T(2, 53, 15)).Should().Be(1, "the slot in progress is not cut short");
        c.NowUtc.Should().BeCloseTo(t.UtcNow, TimeSpan.FromMilliseconds(10), "re-anchored at the boundary");
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(-0.8)]
    public void Poll_ClockCorrectedMidSlot_FinishesSlotThenFollowsCorrection(double seconds)
    {
        var time = new ManualClock(T(2, 53, 14.9));
        var corrected = new CorrectedClock(time);
        var c = new SlotClock(corrected, Mode.Ft8);
        var ev = new List<SlotEvent>();
        c.Event += ev.Add;
        c.Poll();
        Run(c, time, TimeSpan.FromSeconds(3));
        corrected.SetCorrection(TimeSpan.FromSeconds(seconds));
        Run(c, time, TimeSpan.FromSeconds(45));
        var starts = ev.Where(e => e.Mark == SlotMark.Start).Select(e => e.SlotStartUtc).ToList();
        starts.Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
        starts.Should().Contain([T(2, 53, 15), T(2, 53, 30), T(2, 53, 45)]);
        ev.Count(e => e.Mark == SlotMark.CaptureCutoff && e.SlotStartUtc == T(2, 53, 15)).Should().Be(1, "the slot in progress is not cut short");
        c.NowUtc.Should().BeCloseTo(time.UtcNow.AddSeconds(seconds), TimeSpan.FromMilliseconds(10), "slots follow the corrected time");
        var last = ev.Last(e => e.Mark == SlotMark.Start);
        (c.NowUtc - last.SlotStartUtc).Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void SetMode_Ft4_TakesEffectAtNextBoundary()
    {
        var (c, t, ev) = Make(T(2, 53, 14.9));
        Run(c, t, TimeSpan.FromSeconds(1));
        c.SetMode(Mode.Ft4);
        Run(c, t, TimeSpan.FromSeconds(30));
        var starts = ev.Where(e => e.Mark == SlotMark.Start).Select(e => (e.SlotStartUtc, e.Mode)).ToList();
        starts[0].Should().Be((T(2, 53, 15), Mode.Ft8));
        starts[1].Should().Be((T(2, 53, 30), Mode.Ft4));
        starts[2].Should().Be((T(2, 53, 37.5), Mode.Ft4));
    }

    [Fact]
    public void Recorder_ShortSlot_ZeroPadsToDecoderLength()
    {
        var r = new SlotRecorder();
        r.Cutoff().Should().BeNull();
        r.BeginSlot(T(2, 53, 15), Mode.Ft8);
        r.Append(Enumerable.Repeat(0.5f, 1000).ToArray());
        var a = r.Cutoff()!;
        a.Samples12k.Length.Should().Be(180_000);
        a.Samples12k[999].Should().Be(16384);
        a.Samples12k[1000].Should().Be(0);
        r.BeginSlot(T(2, 53, 30), Mode.Ft4);
        r.Cancel();
        r.Cutoff().Should().BeNull("a transmit slot is not decoded");
    }

    [Fact]
    public void Snapshot_MidSlot_CopiesSoFarAndKeepsRecording()
    {
        var r = new SlotRecorder();
        r.Snapshot().Should().BeNull();
        r.BeginSlot(T(2, 53, 15), Mode.Ft8);
        r.Append(Enumerable.Repeat(0.5f, 1000).ToArray());

        var early = r.Snapshot()!;
        r.Append(Enumerable.Repeat(0.25f, 1000).ToArray());
        var full = r.Cutoff()!;

        early.SlotStartUtc.Should().Be(T(2, 53, 15));
        early.Samples12k.Length.Should().Be(180_000);
        early.Samples12k[999].Should().Be(16384);
        early.Samples12k[1000].Should().Be(0, "audio after the snapshot is not in it");
        full.Samples12k[1999].Should().Be(8192, "the slot went on recording");
    }
}
