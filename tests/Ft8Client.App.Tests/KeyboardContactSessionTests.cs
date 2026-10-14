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
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Modems.Psk;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class KeyboardContactSessionTests : IDisposable
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

    private sealed class Log : ISessionLog
    {
        public List<(ContactLogEntry Entry, LogContext Context)> Written { get; } = [];

        public bool Write(ContactLogEntry entry, LogContext context)
        {
            Written.Add((entry, context));
            return true;
        }
    }

    private const string Partner = "KP4JRS";
    private const int PartnerHz = 1180;
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc));
    private readonly SimulatedRig _rig;
    private readonly NullAudioOutput _out;
    private readonly Transmitter _tx;
    private readonly Session _session;
    private readonly KeyboardSession _keyboard;
    private readonly Log _log = new();
    private readonly SessionConfig _config = new()
    {
        MyCall = "W7LIT", MyGrid = "DN40", Band = "20m", Mode = Mode.Psk31,
    };

    public KeyboardContactSessionTests()
    {
        var freqs = ReferenceData.LoadFrequencies();
        _rig = new SimulatedRig(_clock);
        _out = new NullAudioOutput { Now = () => _clock.UtcNow };
        _tx = new Transmitter(_rig, _out, _clock, freqs);
        _session = new Session(_config, new NoDecoder(), new NoAudio(), _tx, _clock, ReferenceData.LoadCountries(), freqs, _log);
        _keyboard = new KeyboardSession(_session, _clock);
    }

    public void Dispose() => _keyboard.Dispose();

    // Their signal, in 100 ms pieces as the capture pump would deliver it.
    private void Hear(string text)
    {
        var random = new Random(1);
        var audio = new PskModulator(Mode.Psk31, ModeInfo.DecoderSampleRate, PartnerHz, 0.3).Modulate(text)
            .Select(s => s + (float)((random.NextDouble() - 0.5) * 0.02)).ToArray();
        for (var i = 0; i < audio.Length; i += 1200)
        {
            _keyboard.Input.Write(audio.AsSpan(i, Math.Min(1200, audio.Length - i)));
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            _keyboard.Drain();
        }
        // The line ends after a pause.
        _clock.Advance(TimeSpan.FromSeconds(4));
        _keyboard.Drain();
    }

    // Plays my over to its end, as the radio's audio device would.
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

    private string Sent => string.Concat(_session.Snapshot.Conversation.Where(l => l.Mine).Select(l => l.Text + "|"));

    [Fact]
    public void CallStation_AnsweringPskCq_WholeContactLogged()
    {
        Hear("CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n");
        _rig.PttLog.Should().BeEmpty();

        _session.CallStation(Partner).Should().BeTrue();
        _rig.PttLog.Should().ContainSingle().Which.On.Should().BeTrue();
        SendOver();
        Sent.Should().Be("KP4JRS DE W7LIT W7LIT K|");
        var c = _session.Snapshot.Contact!;
        c.DxCall.Should().Be(Partner);
        c.Steps.Select(s => s.State).Should().Equal(StepState.Done, StepState.Done, StepState.Now, StepState.Upcoming, StepState.Upcoming);

        Hear("W7LIT DE KP4JRS GE OM UR 579 579 NAME JOSE JOSE QTH SAN JUAN. HW? W7LIT DE KP4JRS KN\n");
        _session.Snapshot.Contact!.ReportReceived.Should().Be(579);
        _session.Snapshot.NextMacro.Should().Be(Macro.ExchangeKey);

        // My name and QTH are not set yet: nothing goes out.
        _session.SendMacro(Macro.ExchangeKey).Should().Contain("{MYNAME}").And.Contain("{MYQTH}");
        _rig.PttLog.Should().HaveCount(2);

        _session.Reconfigure(_config with { MyName = "Tony", MyQth = "Boise" });
        _session.KeyEnter(string.Empty).Should().BeNull();
        SendOver();
        Sent.Should().EndWith("KP4JRS DE W7LIT GE Jose UR 599 599 NAME Tony Tony QTH Boise HW? KP4JRS DE W7LIT KN|");

        Hear("TNX TONY 73 W7LIT DE KP4JRS SK\n");

        var (entry, ctx) = _log.Written.Should().ContainSingle().Subject;
        entry.Call.Should().Be(Partner);
        entry.ReportSent.Should().Be(599);
        entry.ReportReceived.Should().Be(579);
        ctx.Mode.Should().Be(Mode.Psk31);
        ctx.Name.Should().Be("Jose");
        ctx.Qth.Should().Be("San Juan");
        (ctx.FrequencyHz - _session.Snapshot.DialHz).Should().BeInRange(PartnerHz - 3, PartnerHz + 3);
        _session.Snapshot.Contact!.Outcome.Should().Be(ContactOutcome.Logged);
        var theirs = string.Join(' ', _session.Snapshot.Conversation.Where(l => !l.Mine).Select(l => l.Text));
        theirs.Should().StartWith("CQ CQ CQ DE KP4JRS").And.Contain("NAME JOSE").And.EndWith("SK");
    }

    [Fact]
    public void ToggleCq_Answered_ContactStartsOnTheirCall()
    {
        _session.SetTxOffset(1600);
        _session.ToggleCq();
        _rig.PttLog.Should().ContainSingle();
        _session.Snapshot.CallingCq.Should().BeTrue();
        SendOver();
        Sent.Should().Be("CQ CQ CQ DE W7LIT W7LIT W7LIT DN40 K|");

        Hear("W7LIT W7LIT DE KP4JRS KP4JRS K\n");

        var snap = _session.Snapshot;
        snap.CallingCq.Should().BeFalse();
        snap.Contact!.DxCall.Should().Be(Partner);
        snap.Contact.Flow.Should().Be(ContactFlow.CalledCq);
        snap.NextMacro.Should().Be(Macro.ExchangeKey);
    }

    [Fact]
    public void KeyEnter_TypedText_StaysOnAirUntilEnterOnEmptyLine()
    {
        _session.KeyEnter("hello").Should().BeNull();
        _session.Snapshot.KeyboardTx.Active.Should().BeTrue();
        _session.TypeKey('!').Should().BeTrue();

        _session.KeyEnter(string.Empty);
        _session.Snapshot.KeyboardTx.Active.Should().BeTrue();
        _session.KeyEnter(string.Empty);
        SendOver();

        Sent.Should().Be("hello!|");
        _rig.PttLog.Select(p => p.On).Should().Equal(true, false);
    }

    [Fact]
    public async Task AbandonAsync_DuringOver_ReleasesPttAndEndsContact()
    {
        Hear("CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n");
        _session.CallStation(Partner);
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        _tx.Poll();

        await _session.AbandonAsync();

        _rig.PttLog.Select(p => p.On).Should().Equal(true, false);
        _session.Snapshot.Contact!.Outcome.Should().Be(ContactOutcome.Abandoned);
        _log.Written.Should().BeEmpty();
    }

    [Fact]
    public void SendMacro_SlottedMode_DoesNothing()
    {
        _session.SetBand("20m", Mode.Ft8);
        _session.SendMacro(Macro.CqKey).Should().BeNull();
        _rig.PttLog.Should().BeEmpty();
    }
}
