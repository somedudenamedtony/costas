// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Reflection;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

public class MiniViewModelTests
{
    private static readonly DateTime Slot = new(2026, 10, 11, 20, 29, 30, DateTimeKind.Utc);

    public class Recorder : DispatchProxy
    {
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOperateCommands.Call)) Calls.Add((string)args![0]!);
            return null;
        }
    }

    private static RankedStation Ranked(string call, NeedTag need, int snr) =>
        new(new Station { Call = call, LastHeardUtc = Slot.AddSeconds(-15), State = StationState.CallingCq, LastSnr = snr },
            new NeedResult(need, 1), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static ContactView InContact() => new("N4OPI", "EM73", ContactFlow.AnswerCq, ContactOutcome.InProgress, -14, null,
    [
        new ContactStep(true, "N4OPI W7LIT DN40", StepState.Done, 1, Slot.AddSeconds(-29.5)),
        new ContactStep(false, "W7LIT N4OPI -12", StepState.Done, 1, Slot.AddSeconds(-15)),
        new ContactStep(true, "N4OPI W7LIT R-14", StepState.Now, 0, null),
        new ContactStep(false, "W7LIT N4OPI RR73", StepState.Upcoming, 0, null),
        new ContactStep(true, "N4OPI W7LIT 73", StepState.Upcoming, 0, null),
    ], false, false, string.Empty);

    private static (MiniViewModel Mini, Recorder Commands, List<int> Halts) Build(SessionSnapshot s, DateTime now)
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        var operate = new OperateViewModel(commands, () => now);
        operate.Apply(s, [], 0);
        var halts = new List<int>();
        var mini = new MiniViewModel(operate, commands, () => halts.Add(1));
        mini.Refresh("20 m", "FT8");
        return (mini, (Recorder)(object)commands, halts);
    }

    [Fact]
    public void Refresh_Idle_ShowsTopOfLine()
    {
        var s = new SessionSnapshot
        {
            Mode = Mode.Ft8,
            Rank = new RankResult([Ranked("JA6VQA", NeedTag.NewCountry, -14), Ranked("K4TLJ", NeedTag.NewGrid, -11)], [], [], []),
        };

        var (mini, _, _) = Build(s, Slot.AddSeconds(6));

        mini.BandMode.Should().Be("20 m FT8");
        mini.SlotText.Should().Be("Receiving · 9 s");
        mini.CountText.Should().Be("2 in line");
        mini.HasTop.Should().BeTrue();
        mini.InContact.Should().BeFalse();
        mini.Call.Should().Be("JA6VQA");
        mini.Why.Should().Be("New country");
        mini.Detail.Should().EndWith("-14");
    }

    [Fact]
    public void Refresh_NothingHeard_ShowsEmptyAndNoCall()
    {
        var (mini, _, _) = Build(new SessionSnapshot { Mode = Mode.Ft8 }, Slot.AddSeconds(6));

        mini.Empty.Should().BeTrue();
        mini.HasTop.Should().BeFalse();
        mini.CountText.Should().BeEmpty();
    }

    [Fact]
    public void CallTop_Idle_CallsTopStationOnly()
    {
        var s = new SessionSnapshot { Mode = Mode.Ft8, Rank = new RankResult([Ranked("JA6VQA", NeedTag.NewCountry, -14)], [], [], []) };
        var (mini, commands, _) = Build(s, Slot.AddSeconds(6));

        mini.CallTopCommand.Execute(null);

        commands.Calls.Should().Equal("JA6VQA");
    }

    [Fact]
    public void CallTop_NobodyInLine_DoesNothing()
    {
        var (mini, commands, _) = Build(new SessionSnapshot { Mode = Mode.Ft8 }, Slot.AddSeconds(6));

        mini.CallTopCommand.Execute(null);

        commands.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Refresh_InContact_ShowsProgressAndMessageOnAir()
    {
        var c = InContact();
        var s = new SessionSnapshot { Mode = Mode.Ft8, Contact = c, Transmitting = true, TransmittingMessage = "N4OPI W7LIT R-14",
            Rank = new RankResult([Ranked("JA6VQA", NeedTag.NewCountry, -14)], [], [], []) };

        var (mini, _, _) = Build(s, Slot.AddSeconds(4));

        mini.InContact.Should().BeTrue();
        mini.HasTop.Should().BeFalse();
        mini.Call.Should().Be("N4OPI");
        mini.Steps.Should().HaveCount(5);
        mini.Message.Should().Be("N4OPI W7LIT R-14");
        mini.SlotText.Should().Be("Transmitting · 11 s");
        mini.SlotKind.Should().Be(TextKind.Critical);
    }

    [Fact]
    public void CallTop_InContact_DoesNothing()
    {
        var s = new SessionSnapshot { Mode = Mode.Ft8, Contact = InContact(), Rank = new RankResult([Ranked("JA6VQA", NeedTag.NewCountry, -14)], [], [], []) };
        var (mini, commands, _) = Build(s, Slot.AddSeconds(4));

        mini.CallTopCommand.Execute(null);

        commands.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Refresh_SomeoneCalling_SaysSoInCaution()
    {
        var s = new SessionSnapshot
        {
            Mode = Mode.Ft8,
            Rank = new RankResult([Ranked("JA6VQA", NeedTag.NewCountry, -14)], [], [], []),
            Waiting = [new WaitingCaller("K6ODE", "CM97", -9, MessageKind.GridReply, null, SlotParity.Even, 1200, Slot, Slot)],
        };

        var (mini, _, _) = Build(s, Slot.AddSeconds(6));

        mini.CountText.Should().Be("1 calling you");
        mini.CountKind.Should().Be(TextKind.Caution);
    }

    [Fact]
    public void Halt_Always_CallsHalt()
    {
        var (mini, _, halts) = Build(new SessionSnapshot { Mode = Mode.Ft8 }, Slot.AddSeconds(6));

        mini.HaltCommand.Execute(null);

        halts.Should().HaveCount(1);
    }

    [Fact]
    public void Expand_Always_RaisesExpandRequested()
    {
        var (mini, _, _) = Build(new SessionSnapshot { Mode = Mode.Ft8 }, Slot.AddSeconds(6));
        var raised = 0;
        mini.ExpandRequested += () => raised++;

        mini.ExpandCommand.Execute(null);

        raised.Should().Be(1);
    }
}
