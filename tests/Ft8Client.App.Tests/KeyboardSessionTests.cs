// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class KeyboardSessionTests : IDisposable
{
    private sealed class NoAudio : ISlotSource
    {
        public void BeginSlot(DateTime slotStartUtc, Mode mode)
        {
        }

        public void CancelSlot()
        {
        }

        public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode) => null;
    }

    private sealed class NoDecoder : IDecoder
    {
        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly ManualClock _clock = new(new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc));
    private readonly SimulatedRig _rig;
    private readonly Session _session;
    private readonly KeyboardSession _keyboard;

    public KeyboardSessionTests()
    {
        var freqs = ReferenceData.LoadFrequencies();
        _rig = new SimulatedRig(_clock);
        var tx = new Transmitter(_rig, new NullAudioOutput { Now = () => _clock.UtcNow }, _clock, freqs);
        _session = new Session(new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Psk31 }, new NoDecoder(),
            new NoAudio(), tx, _clock, ReferenceData.LoadCountries(), freqs);
        _keyboard = new KeyboardSession(_session, _clock);
    }

    public void Dispose() => _keyboard.Dispose();

    // Feeds audio in 100 ms pieces as the capture pump would, moving the clock with it.
    private void Feed(float[] audio)
    {
        for (var i = 0; i < audio.Length; i += 1200)
        {
            _keyboard.Input.Write(audio.AsSpan(i, Math.Min(1200, audio.Length - i)));
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            _keyboard.Drain();
        }
    }

    private static float[] Psk31(double hz, string text)
    {
        var random = new Random(1);
        var signal = new PskModulator(Mode.Psk31, ModeInfo.DecoderSampleRate, hz, 0.3).Modulate(text);
        return [.. signal.Select(s => s + (float)((random.NextDouble() - 0.5) * 0.02))];
    }

    [Fact]
    public void Drain_CqHeard_StationListedAndCallable()
    {
        Feed(Psk31(1180, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"));

        var snap = _session.Snapshot;
        var line = snap.Rank.Line.Should().ContainSingle().Which;
        line.Station.Call.Should().Be("KP4JRS");
        line.Station.State.Should().Be(StationState.CallingCq);
        line.Station.OffsetHz.Should().BeInRange(1177, 1183);
        line.Station.RecentText.Should().Contain("CQ CQ CQ DE KP4JRS");
        snap.RawDecodes.Should().Contain(r => r.Decode.Text.Contains("DE KP4JRS", StringComparison.Ordinal));
    }

    private static float[] Rtty(RttyFormat format, double hz, string text)
    {
        var random = new Random(1);
        var signal = new RttyModulator(format, ModeInfo.DecoderSampleRate, hz, 0.3).Modulate(text);
        return [.. signal.Select(s => s + (float)((random.NextDouble() - 0.5) * 0.02))];
    }

    [Fact]
    public void Drain_RttyCqHeard_StationListedAtItsCentre()
    {
        _session.SetBand("20m", Mode.Rtty);
        Feed(Rtty(RttyFormat.Standard, 1500, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"));

        var line = _session.Snapshot.Rank.Line.Should().ContainSingle().Which;
        line.Station.Call.Should().Be("KP4JRS");
        line.Station.State.Should().Be(StationState.CallingCq);
        line.Station.OffsetHz.Should().BeInRange(1495, 1505);
    }

    [Fact]
    public void SetRtty_Reverse_ReadsAStationSendingUpsideDown()
    {
        var upsideDown = RttyFormat.Standard with { Reverse = true };
        _session.SetBand("20m", Mode.Rtty);
        Feed(Rtty(upsideDown, 1500, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"));
        _session.Snapshot.Stations.Should().BeEmpty();

        _keyboard.SetRtty(upsideDown, unshiftOnSpace: true);
        Feed(Rtty(upsideDown, 1500, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"));
        _session.Snapshot.Stations.Should().ContainSingle().Which.Call.Should().Be("KP4JRS");
    }

    [Fact]
    public void Drain_SlottedMode_ReadsNothing()
    {
        _session.SetBand("20m", Mode.Ft8);
        Feed(Psk31(1180, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"));
        _session.Snapshot.Stations.Should().BeEmpty();
    }

    [Fact]
    public void Resend_KeyboardMode_RefusedWithoutKeying()
    {
        Feed(Psk31(1180, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"));

        _session.Resend();

        _session.Snapshot.Fault.Should().Contain("macro keys");
        _rig.PttLog.Should().BeEmpty();
    }

    [Fact]
    public void OnSlotEvent_KeyboardMode_Ignored()
    {
        var act = () => _session.OnSlotEvent(new SlotEvent(SlotMark.CaptureCutoff, _clock.UtcNow, Mode.Ft8, TimeSpan.Zero));
        act.Should().NotThrow();
    }
}
