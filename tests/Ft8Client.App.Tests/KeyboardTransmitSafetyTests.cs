// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.Data.Log;
using Ft8Client.App.Engine;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

// Transmit guards around PSK and RTTY overs at the session: a radio that refuses PTT, repeated macro keys, a band change
// or dial move under a transmission, and the hold while the radio is set up for the mode. Simulated radio only.
public sealed class KeyboardTransmitSafetyTests : IDisposable
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

    private sealed class NoLog : ISessionLog
    {
        public QsoRecord? Write(ContactLogEntry entry, LogContext context) => null;
    }

    private readonly ManualClock _clock = new(new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc));
    private readonly SimulatedRig _rig;
    private readonly NullAudioOutput _out;
    private readonly Transmitter _tx;
    private readonly Session _session;

    public KeyboardTransmitSafetyTests()
    {
        var freqs = ReferenceData.LoadFrequencies();
        _rig = new SimulatedRig(_clock);
        _out = new NullAudioOutput { Now = () => _clock.UtcNow };
        _tx = new Transmitter(_rig, _out, _clock, freqs);
        var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Psk31 };
        _session = new Session(config, new NoDecoder(), new NoAudio(), _tx, _clock, ReferenceData.LoadCountries(), freqs, new NoLog());
        _session.SetDial(14_070_000);
    }

    public void Dispose() => _session.Keys.Dispose();

    private void Play(TimeSpan d)
    {
        var end = _clock.UtcNow + d;
        while (_clock.UtcNow < end)
        {
            for (var i = 0; i < 10; i++)
            {
                _clock.Advance(TimeSpan.FromMilliseconds(1));
                _tx.Poll();
            }
            _session.Keys.Pump();
            _out.Pull(_tx.OutputSampleRate / 100);
        }
    }

    [Fact]
    public void SendMacro_PttRefusedByRadio_FaultShownAndKeyboardFree()
    {
        _rig.FailNext = "CAT timeout";

        var why = _session.SendMacro(Macro.CqKey);

        why.Should().Contain("PTT could not be asserted");
        _session.Snapshot.Fault.Should().Contain("CAT timeout");
        _session.Keys.Active.Should().BeFalse();
        _session.IsTransmitting.Should().BeFalse();
        _rig.Ptt.Should().BeFalse();

        // Not stuck: the next press sends.
        _session.SendMacro(Macro.CqKey).Should().BeNull();
        _session.Keys.Active.Should().BeTrue();
    }

    [Fact]
    public void SendMacro_SameMacroStillWaiting_RefusedNotQueuedTwice()
    {
        _session.SendMacro(Macro.CqKey).Should().BeNull();
        var waiting = _session.Keys.View.Waiting;

        _session.SendMacro(Macro.CqKey).Should().Contain("already waiting");

        _session.Keys.View.Waiting.Should().Be(waiting);
    }

    [Fact]
    public async Task SetBand_TuneToneOnAir_ToneStopped()
    {
        _session.SetBand("20m", Mode.Ft8);
        _session.SetDial(14_074_000);
        (await _session.TuneAsync(10)).Should().BeNull();
        _tx.Transmitting.Should().BeTrue();

        _session.SetBand("40m", Mode.Ft8);

        _tx.Transmitting.Should().BeFalse();
        _rig.Ptt.Should().BeFalse();
    }

    [Fact]
    public void SetBand_KeyboardOverOnAir_OverEndedAndRadioReleased()
    {
        _session.SendMacro(Macro.CqKey).Should().BeNull();
        Play(TimeSpan.FromSeconds(1));

        _session.SetBand("40m", Mode.Psk31);

        _session.Keys.Active.Should().BeFalse();
        _rig.Ptt.Should().BeFalse();
    }

    [Fact]
    public void SetDial_MovedPastBandEdgeDuringOver_TransmissionStopped()
    {
        _session.SendMacro(Macro.CqKey).Should().BeNull();
        Play(TimeSpan.FromSeconds(1));

        _session.SetDial(14_349_500);

        _rig.Ptt.Should().BeFalse();
        _session.Keys.Active.Should().BeFalse();
        _session.Snapshot.Fault.Should().Contain("dial moved");
    }

    [Fact]
    public void SetDial_MovedWithinBandDuringOver_KeepsSending()
    {
        _session.SendMacro(Macro.CqKey).Should().BeNull();
        Play(TimeSpan.FromSeconds(1));

        _session.SetDial(14_071_000);

        _rig.Ptt.Should().BeTrue();
    }

    [Fact]
    public void SendMacro_HeldWhileRadioIsSetUp_RefusedWithReason()
    {
        _session.HoldKeyboardTransmit("Setting the RTTY power cap on the radio; send again in a moment.");

        _session.SendMacro(Macro.CqKey).Should().Contain("power cap");
        _rig.PttLog.Should().BeEmpty();

        _session.HoldKeyboardTransmit(null);
        _session.SendMacro(Macro.CqKey).Should().BeNull();
    }

    [Fact]
    public void SendMacro_PskOnBandWithoutPskRow_RefusedWithReason()
    {
        _session.SetBand("60m", Mode.Psk31);
        _session.SetDial(5_357_000);

        _session.SendMacro(Macro.CqKey).Should().Contain("no PSK31 row");
        _rig.PttLog.Should().BeEmpty();
    }
}
