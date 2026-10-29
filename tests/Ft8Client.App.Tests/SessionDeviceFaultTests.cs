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

public class SessionDeviceFaultTests
{
    private const string Removed = "The audio device has been disconnected or the audio hardware has been reconfigured.";

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
    private readonly NullAudioOutput _output;
    private readonly Transmitter _tx;
    private readonly Session _session;

    public SessionDeviceFaultTests()
    {
        var freqs = ReferenceData.LoadFrequencies();
        _output = new NullAudioOutput { Now = () => _clock.UtcNow };
        _tx = new Transmitter(new SimulatedRig(_clock), _output, _clock, freqs);
        _session = new Session(new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Ft8 }, new NoDecoder(),
            new NoAudio(), _tx, _clock, ReferenceData.LoadCountries(), freqs);
    }

    [Fact]
    public void ClearFault_AudioOutputBack_ClearsItsFault()
    {
        _output.RaiseFault(Removed);
        _session.Snapshot.Fault.Should().Contain("audio output stopped").And.Contain("when the device is back");

        _tx.SetOutput(new NullAudioOutput { Now = () => _clock.UtcNow });
        _session.ClearFault(Removed);

        _session.Snapshot.Fault.Should().BeNull();
        _tx.OutputFault.Should().BeNull();
    }

    [Fact]
    public void ClearFault_DifferentCause_KeepsFault()
    {
        _output.RaiseFault(Removed);

        _session.ClearFault("Audio output stopped.");

        _session.Snapshot.Fault.Should().Contain(Removed);
    }
}
