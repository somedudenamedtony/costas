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
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class SessionTuneTests
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

    private readonly ManualClock _clock = new(new DateTime(2026, 1, 4, 2, 53, 59, DateTimeKind.Utc));
    private readonly SimulatedRig _rig;
    private readonly Transmitter _tx;
    private readonly Session _session;

    public SessionTuneTests()
    {
        var freqs = ReferenceData.LoadFrequencies();
        _rig = new SimulatedRig(_clock);
        _tx = new Transmitter(_rig, new NullAudioOutput { Now = () => _clock.UtcNow }, _clock, freqs);
        _session = new Session(new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Ft8 }, new NoDecoder(),
            new NoAudio(), _tx, _clock, ReferenceData.LoadCountries(), freqs);
    }

    [Fact]
    public async Task TuneAsync_Idle_KeysAndShowsTune()
    {
        var why = await _session.TuneAsync(10);

        why.Should().BeNull();
        _rig.Ptt.Should().BeTrue();
        _session.Snapshot.TransmittingMessage.Should().Be(Session.TuneMessage);
    }

    [Fact]
    public async Task TuneAsync_ToneEnds_ReleasesPtt()
    {
        await _session.TuneAsync(2);
        for (var i = 0; i < 300; i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(10));
            _tx.Poll();
        }

        _rig.Ptt.Should().BeFalse();
        _tx.Transmitting.Should().BeFalse();
    }

    [Fact]
    public async Task TuneAsync_WhileCallingCq_RefusedWithoutKeying()
    {
        _session.ToggleCq();

        var why = await _session.TuneAsync(10);

        why.Should().NotBeNull();
        _rig.PttLog.Should().NotContain(p => p.On);
    }

    [Fact]
    public async Task HaltAsync_DuringTune_ReleasesPtt()
    {
        await _session.TuneAsync(10);

        await _session.HaltAsync();

        _rig.Ptt.Should().BeFalse();
        _tx.Transmitting.Should().BeFalse();
    }
}
