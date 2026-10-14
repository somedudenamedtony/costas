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
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Modems.Psk;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

// Enter in PSK and RTTY as the screen sends it: refusals change nothing, and an Enter aimed at an over that has just
// ended does not start another.
public sealed class KeyboardEnterSessionTests : IDisposable
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
        public bool Write(ContactLogEntry entry, LogContext context) => true;
    }

    private readonly ManualClock _clock = new(new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc));
    private readonly SimulatedRig _rig;
    private readonly NullAudioOutput _out;
    private readonly Transmitter _tx;
    private readonly Session _session;
    private readonly KeyboardSession _keyboard;

    public KeyboardEnterSessionTests()
    {
        var freqs = ReferenceData.LoadFrequencies();
        _rig = new SimulatedRig(_clock);
        _out = new NullAudioOutput { Now = () => _clock.UtcNow };
        _tx = new Transmitter(_rig, _out, _clock, freqs);
        var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Psk31 };
        _session = new Session(config, new NoDecoder(), new NoAudio(), _tx, _clock, ReferenceData.LoadCountries(), freqs, new NoLog());
        _keyboard = new KeyboardSession(_session, _clock);
    }

    public void Dispose() => _keyboard.Dispose();

    // A station on the offset the CQ would otherwise use, so a clear spot elsewhere is picked.
    private void Hear(string text, int hz)
    {
        var random = new Random(1);
        var audio = new PskModulator(Mode.Psk31, ModeInfo.DecoderSampleRate, hz, 0.3).Modulate(text)
            .Select(s => s + (float)((random.NextDouble() - 0.5) * 0.02)).ToArray();
        for (var i = 0; i < audio.Length; i += 1200)
        {
            _keyboard.Input.Write(audio.AsSpan(i, Math.Min(1200, audio.Length - i)));
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            _keyboard.Drain();
        }
        _clock.Advance(TimeSpan.FromSeconds(4));
        _keyboard.Drain();
    }

    private void SendOver()
    {
        var end = _clock.UtcNow + TimeSpan.FromSeconds(60);
        while (_tx.Transmitting && _clock.UtcNow < end)
        {
            for (var i = 0; i < 10; i++)
            {
                _clock.Advance(TimeSpan.FromMilliseconds(1));
                _tx.Poll();
            }
            _session.Keys.Pump();
            _out.Pull(_tx.OutputSampleRate / 100);
        }
        _tx.Transmitting.Should().BeFalse();
    }

    [Fact]
    public void KeyEnter_RefusedText_KeepsTxOffset()
    {
        Hear("CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n", 1500);
        var before = _session.Snapshot.TxOffsetHz;

        _session.KeyEnter("hello é").Should().NotBeNull();

        _session.Snapshot.TxOffsetHz.Should().Be(before);
        _rig.PttLog.Should().BeEmpty();
    }

    [Fact]
    public void KeyEnter_ScreenShowedOverThatHasEnded_SendsNothing()
    {
        _session.KeyEnter("hello").Should().BeNull();
        _session.KeyEnter(string.Empty, onAir: true);
        SendOver();

        _session.KeyEnter(string.Empty, onAir: true).Should().BeNull();

        _session.Snapshot.KeyboardTx.Active.Should().BeFalse();
        _rig.PttLog.Select(p => p.On).Should().Equal(true, false);
    }

    [Fact]
    public void KeyEnter_TextTypedAsOverStarts_JoinsOver()
    {
        _session.KeyEnter("hello").Should().BeNull();

        _session.KeyEnter(" there").Should().BeNull();
        _session.KeyEnter(string.Empty, onAir: true);
        _session.KeyEnter(string.Empty, onAir: true);
        SendOver();

        _session.Snapshot.Conversation.Should().ContainSingle().Which.Text.Should().Be("hello there");
    }

    [Fact]
    public async Task AbandonAsync_PartnerKeepsSending_ConversationStopsAndModeKept()
    {
        Hear("CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n", 1180);
        _session.CallStation("KP4JRS").Should().BeTrue();
        await _session.AbandonAsync();
        var lines = _session.Snapshot.Conversation.Count;

        Hear("CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n", 1180);
        _session.Snapshot.Conversation.Should().HaveCount(lines);
        _session.SetBand("20m", Mode.Rtty);

        _session.Snapshot.Contact?.Mode.Should().Be(Mode.Psk31);
    }
}
