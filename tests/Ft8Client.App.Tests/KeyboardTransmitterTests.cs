// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.Engine;
using Ft8Client.Audio.Backends;
using Ft8Client.Core;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Modems;
using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class KeyboardTransmitterTests
{
    private const int Offset = 1000;

    private sealed class Station
    {
        public readonly ManualClock Clock = new(new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc));
        public readonly SimulatedRig Radio;
        public readonly NullAudioOutput Out;
        public readonly Transmitter Tx;
        public readonly KeyboardTransmitter Keys;
        public readonly List<KeyboardOver> Overs = [];
        public readonly List<string> Faults = [];

        public Station()
        {
            Radio = new SimulatedRig(Clock);
            Out = new NullAudioOutput { Now = () => Clock.UtcNow };
            Tx = new Transmitter(Radio, Out, Clock, ReferenceData.LoadFrequencies());
            Tx.Fault += Faults.Add;
            Keys = new KeyboardTransmitter(Tx, Clock, background: false);
            Keys.OverEnded += Overs.Add;
        }

        public string? Send(string text, int? macro = null, bool returnToReceive = true, Mode mode = Mode.Psk31, long dial = 14_070_000) =>
            Keys.Send(text, macro, returnToReceive, () => Modulator(mode), s => Tx.StartStream(s, mode, Offset, Guard(dial), TimeSpan.FromMilliseconds(200)));

        // Plays the over in 10 ms steps as the audio device and reader thread would.
        public void Run(TimeSpan d)
        {
            var end = Clock.UtcNow + d;
            while (Clock.UtcNow < end)
            {
                for (var i = 0; i < 10; i++)
                {
                    Clock.Advance(TimeSpan.FromMilliseconds(1));
                    Tx.Poll();
                }
                Keys.Pump();
                Out.Pull(Tx.OutputSampleRate / 100);
            }
        }

        public void RunUntilEnded(TimeSpan limit)
        {
            var end = Clock.UtcNow + limit;
            while (Overs.Count == 0 && Clock.UtcNow < end) Run(TimeSpan.FromMilliseconds(10));
        }

        private IKeyboardModulator Modulator(Mode mode) => mode == Mode.Rtty
            ? new RttyModulator(RttyFormat.Standard, Tx.OutputSampleRate, Offset)
            : new PskModulator(mode, Tx.OutputSampleRate, Offset);

        private static TxGuardInput Guard(long dial) => new("20m", dial, null, 2.0, null, true);
    }

    [Fact]
    public void Send_MacroThatReturnsToReceive_PlaysWholeOverAndReleasesPtt()
    {
        var s = new Station();
        s.Send("CQ CQ DE TEST K ", macro: 1).Should().BeNull();
        s.Radio.PttLog.Should().ContainSingle().Which.On.Should().BeTrue();

        s.RunUntilEnded(TimeSpan.FromSeconds(30));

        var over = s.Overs.Should().ContainSingle().Subject;
        over.Sent.Should().Be("CQ CQ DE TEST K ");
        over.Macros.Should().Equal(1);
        over.Completed.Should().BeTrue();
        over.Watchdog.Should().BeFalse();
        s.Radio.PttLog.Select(p => p.On).Should().Equal(true, false);
        s.Keys.Active.Should().BeFalse();
        s.Tx.Transmitting.Should().BeFalse();
        s.Out.Streamed.Should().Contain(x => Math.Abs(x) > 0.1f);
    }

    [Fact]
    public void Send_AudioStartsAfterPttLead_NoAudioBeforeLead()
    {
        var s = new Station();
        s.Send("TEST ");
        s.Run(TimeSpan.FromMilliseconds(150));
        s.Out.Stream.Should().BeNull();
        s.Run(TimeSpan.FromMilliseconds(100));
        s.Out.Stream.Should().NotBeNull();
    }

    [Fact]
    public void Send_RttyOver_ReportsCapitals()
    {
        var s = new Station();
        s.Send("cq test", mode: Mode.Rtty).Should().BeNull();
        s.RunUntilEnded(TimeSpan.FromSeconds(30));
        s.Overs.Single().Sent.Should().Be("CQ TEST");
    }

    [Fact]
    public void Send_CharacterTheModeCannotSend_RefusedWithReasonAndNothingKeyed()
    {
        var s = new Station();
        var why = s.Send("café");
        why.Should().Contain("cannot send").And.Contain("'é'");
        s.Radio.PttLog.Should().BeEmpty();
        s.Keys.Active.Should().BeFalse();
    }

    [Fact]
    public void Send_OutsideBand_RefusedAndNothingKeyed()
    {
        var s = new Station();
        var why = s.Send("TEST", dial: 14_400_000);
        why.Should().Contain("outside");
        s.Radio.PttLog.Should().BeEmpty();
        s.Keys.Active.Should().BeFalse();
    }

    [Fact]
    public async Task HaltAsync_DuringOver_ReleasesPttAndReportsCut()
    {
        var s = new Station();
        s.Send("THIS IS A LONG OVER THAT WILL NOT FINISH BEFORE HALT ");
        s.Run(TimeSpan.FromSeconds(1));

        await s.Tx.HaltAsync();

        s.Radio.PttLog.Select(p => p.On).Should().Equal(true, false);
        var over = s.Overs.Should().ContainSingle().Subject;
        over.Completed.Should().BeFalse();
        s.Keys.Active.Should().BeFalse();
        s.Keys.Pump().Should().BeFalse();
    }

    [Fact]
    public void InjectFault_RadioFaultDuringOver_ReleasesPtt()
    {
        var s = new Station();
        s.Send("THIS IS A LONG OVER THAT WILL NOT FINISH ");
        s.Run(TimeSpan.FromSeconds(1));

        s.Radio.InjectFault("CAT timeout");

        s.Radio.PttLog.Last().On.Should().BeFalse();
        s.Faults.Should().ContainSingle().Which.Should().Contain("CAT timeout");
        s.Overs.Single().Completed.Should().BeFalse();
    }

    [Fact]
    public void RaiseFault_AudioFaultDuringOver_ReleasesPtt()
    {
        var s = new Station();
        s.Send("THIS IS A LONG OVER THAT WILL NOT FINISH ");
        s.Run(TimeSpan.FromSeconds(1));

        s.Out.RaiseFault("device removed");

        s.Radio.PttLog.Last().On.Should().BeFalse();
        s.Overs.Single().Completed.Should().BeFalse();
    }

    [Fact]
    public void Pump_IdleTwentySeconds_WatchdogClosesOver()
    {
        var s = new Station();
        s.Send("TEST ", returnToReceive: false);
        s.Run(TimeSpan.FromSeconds(15));
        s.Overs.Should().BeEmpty();

        s.RunUntilEnded(TimeSpan.FromSeconds(10));

        var over = s.Overs.Should().ContainSingle().Subject;
        over.Watchdog.Should().BeTrue();
        over.Completed.Should().BeFalse();
        s.Radio.PttLog.Last().On.Should().BeFalse();
    }

    [Fact]
    public void Pump_ThreeMinutesOnAir_WatchdogClosesOver()
    {
        var s = new Station();
        s.Send("A", returnToReceive: false);
        for (var i = 0; i < 40 && s.Overs.Count == 0; i++)
        {
            s.Keys.Type('E');
            s.Run(TimeSpan.FromSeconds(5));
        }

        var over = s.Overs.Should().ContainSingle().Subject;
        over.Watchdog.Should().BeTrue();
        s.Radio.PttLog.Select(p => p.On).Should().Equal(true, false);
        (s.Radio.PttLog[1].TimeUtc - s.Radio.PttLog[0].TimeUtc).Should().BeCloseTo(KeyboardTransmitter.MaxOver, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Pump_AudioDeviceStopsTakingSamples_HaltedAfterGrace()
    {
        var s = new Station();
        s.Send("TEST ", returnToReceive: false);
        var limit = KeyboardTransmitter.MaxOver + KeyboardTransmitter.StallGrace;
        // Nothing reads the stream: the over can never play out by itself.
        while (s.Radio.PttLog.Count < 2 && s.Clock.UtcNow - s.Radio.PttLog[0].TimeUtc < limit + TimeSpan.FromSeconds(1))
        {
            s.Clock.Advance(TimeSpan.FromMilliseconds(100));
            s.Tx.Poll();
            s.Keys.Pump();
        }

        s.Radio.PttLog.Select(p => p.On).Should().Equal(true, false);
        (s.Radio.PttLog[1].TimeUtc - s.Radio.PttLog[0].TimeUtc).Should().BeCloseTo(limit, TimeSpan.FromMilliseconds(200));
        var over = s.Overs.Should().ContainSingle().Subject;
        over.Watchdog.Should().BeTrue();
        s.Keys.Active.Should().BeFalse();
    }

    [Fact]
    public void Backspace_CharacterNotYetSent_IsNotSent()
    {
        var s = new Station();
        s.Send("ABC", returnToReceive: false);
        s.Keys.Type('X').Should().BeTrue();
        s.Keys.Backspace().Should().BeTrue();
        s.Keys.ReturnToReceive();

        s.RunUntilEnded(TimeSpan.FromSeconds(30));

        s.Overs.Single().Sent.Should().Be("ABC");
    }

    [Fact]
    public void Type_NoOverOnAir_ReturnsFalse()
    {
        var s = new Station();
        s.Keys.Type('A').Should().BeFalse();
        s.Radio.PttLog.Should().BeEmpty();
    }
}
