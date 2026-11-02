// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Remote;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;

namespace Ft8Client.App.Tests;

public class RemoteStateBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 20, 31, 6, DateTimeKind.Utc);
    private static readonly RemoteContext Ctx = new() { Bands = m => m == Mode.Ft8 ? ["40m", "20m"] : ["20m"] };

    private static RankedStation Ranked(string call, NeedTag need, bool callable, StationState state = StationState.CallingCq, WatchReason? reason = null) =>
        new(new Station { Call = call, LastHeardUtc = Now.AddSeconds(-20), LastHeardEndUtc = Now.AddSeconds(-20), State = state, CqStreak = 1, LastSnr = -8 },
            new NeedResult(need, 1), new HearsYou(HearsYouKind.None), Chance.Good, callable, reason);

    private static RawDecodeLine Line(string text, bool toMe = false, bool tx = false) =>
        new(new Decode(Now.AddSeconds(-6), -10, 0.1, 1200, text, false), MessageParser.Parse(text), "Canada", toMe, tx);

    private static SessionSnapshot Snap()
    {
        var line = Ranked("VE7XYZ", NeedTag.NewGrid, true);
        var worked = new[] { Ranked("N0XYZ", NeedTag.Worked, false, StationState.InContact, new WatchReason(WatchReasonKind.InContact, "K1AA")),
                             Ranked("W1AW", NeedTag.Worked, true) };
        var watching = Ranked("JA1ABC", NeedTag.NewCountry, false, StationState.InContact, new WatchReason(WatchReasonKind.InContact, "K6XX"));
        return new SessionSnapshot
        {
            MyCall = "W7LIT",
            Band = "20m",
            Mode = Mode.Ft8,
            DialHz = 14_074_000,
            Rank = new RankResult([line], [watching], worked, []),
            Stations = [line.Station, worked[0].Station, worked[1].Station, watching.Station],
            RawDecodes = [Line("CQ VE7XYZ CN89"), Line("W7LIT K5ABC EM10", toMe: true), Line("CQ W7LIT DN40", tx: true)],
            Tonight = [new TonightContact("VE3AAA", "20m", Now.AddMinutes(-30), NeedTag.NewCall, "FT8"), new TonightContact("EA8XX", "40m", Now.AddMinutes(-10), NeedTag.NewCountry, "PSK31")],
            TxOffsetHz = 1200,
            TxOffsetAuto = false,
        };
    }

    [Fact]
    public void Build_Lists_CarryTheOperateColumns()
    {
        var s = RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", null);

        s.Line.Should().ContainSingle().Which.Should().Match<RemoteStation>(r => r.Call == "VE7XYZ" && r.Button == "Call" && r.Chance == "Good" && r.ChanceReason == "no reports yet" && r.LastHeard == "20 seconds ago");
        s.Repeats.Select(r => r.Call).Should().Equal("W1AW", "N0XYZ");
        s.Repeats[1].Button.Should().BeEmpty();
        s.Watching.Should().ContainSingle().Which.Reason.Should().NotBeEmpty();
    }

    [Fact]
    public void Build_StationWithCallers_RowCarriesCallingTag()
    {
        var pile = new Pileup
        {
            Target = "VE7XYZ",
            CallersSlotUtc = Now.AddSeconds(-6),
            Callers = [new PileupCaller("W6ABC", Now.AddSeconds(-6), -5, 1400), new PileupCaller("K7XYZ", Now.AddSeconds(-6), -9, 1500)],
        };
        var snap = Snap() with { Pileups = new Dictionary<string, Pileup> { ["VE7XYZ"] = pile } };

        var s = RemoteStateBuilder.Build(snap, Now, Ctx, "you", null);

        s.Line.Single().Calling.Should().Be("2 calling");
        s.Repeats.Should().OnlyContain(r => r.Calling.Length == 0);
    }

    [Fact]
    public void Build_Decodes_NewestFirstWithKinds()
    {
        var s = RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", null);

        s.Decodes.Select(d => d.Kind).Should().Equal("tx", "me", "cq");
        s.Decodes[0].Sender.Should().BeNull();
        s.Decodes[2].Sender.Should().Be("VE7XYZ");
    }

    [Fact]
    public void Build_Tonight_NewestFirst()
    {
        var s = RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", null);

        s.TonightTitle.Should().Be("Worked this session · 2");
        s.Tonight.Select(t => t.Call).Should().Equal("EA8XX", "VE3AAA");
        s.Tonight[0].Band.Should().Be("40 m PSK31");
        s.Tonight[1].Band.Should().Be("20 m FT8");
    }

    [Fact]
    public void Build_BandAndOffset_FromSnapshotAndContext()
    {
        var s = RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", null);

        s.Band.Should().Be("20m");
        s.Mode.Should().Be("FT8");
        s.Bands.Select(b => b.Name).Should().Equal("40 m", "20 m");
        s.CanChangeBand.Should().BeTrue();
        s.Offset.Should().StartWith("Manual · ");
        s.OffsetHz.Should().Be(1200);
        s.SlotLabel.Should().Be("Receiving");
        s.SlotLengthMs.Should().Be(15_000);
        s.SlotIntoMs.Should().Be(6_000);
    }

    [Fact]
    public void Build_Transmitting_CannotChangeBandAndCountsDownTheSlot()
    {
        var s = RemoteStateBuilder.Build(Snap() with { Transmitting = true }, Now, Ctx, "you", null);

        s.CanChangeBand.Should().BeFalse();
        s.SlotLabel.Should().Be("Transmitting");
        s.SlotLengthMs.Should().Be(15_000);
    }

    [Fact]
    public void Build_AudioEndedInTransmitSlot_StillTransmitSlot()
    {
        var s = RemoteStateBuilder.Build(Snap() with { LastTxSlotUtc = SlotMath.SlotStart(Now, Mode.Ft8) }, Now, Ctx, "you", null);

        s.Transmitting.Should().BeFalse();
        s.SlotLabel.Should().Be("Transmitting");
    }

    [Fact]
    public void Build_MeterWarning_ShownWithAdvice()
    {
        var s = RemoteStateBuilder.Build(Snap() with { TxMeters = new TxMeterResult(0.1, 3.4, TxMeterLevel.Critical, "Check the antenna.") }, Now, Ctx, "you", null);

        s.Meter.Should().Be("SWR 3.4");
        s.MeterTip.Should().Be("Check the antenna.");
        s.MeterCritical.Should().BeTrue();
    }

    [Fact]
    public void Build_Viewing_CallableStationGetsDetailsAndCallButton()
    {
        var d = RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", "VE7XYZ").Details;

        d.Should().NotBeNull();
        d!.Call.Should().Be("VE7XYZ");
        d.Button.Should().Be("Call");
        d.Command.Should().Be("call");
        d.Heard.Should().Contain("-08 dB").And.Contain("20 seconds ago");
    }

    [Fact]
    public void Build_ViewingStationNotHeard_NoButton()
    {
        var d = RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", "VK2ABC").Details!;

        d.Button.Should().BeEmpty();
        d.Heard.Should().BeEmpty();
        d.Need.Should().BeEmpty();
    }

    [Fact]
    public void Build_NotViewing_NoDetails()
    {
        RemoteStateBuilder.Build(Snap(), Now, Ctx, "you", null).Details.Should().BeNull();
    }

    [Fact]
    public void Build_JustLogged_SendsNoticeWithTimeLeft()
    {
        var logged = new LoggedContact(3, "W9TJP", NeedTag.NewCall, "Indiana, USA", "20m", Mode.Ft8, -12, -8, Now.AddSeconds(-2), PowerWatts: "25");

        var l = RemoteStateBuilder.Build(Snap() with { LastLogged = logged }, Now, Ctx, "you", null).Logged!;

        l.Id.Should().Be(3);
        l.Call.Should().Be("W9TJP");
        l.Need.Should().Be("New call");
        l.Line.Should().Be("Sent -12 · Received -08 · Indiana, USA · 25 W");
        l.MsLeft.Should().Be(3000);
    }

    [Fact]
    public void Build_LoggedLongAgo_SendsNoNotice()
    {
        var logged = new LoggedContact(3, "W9TJP", NeedTag.NewCall, "", "20m", Mode.Ft8, -12, -8, Now.AddSeconds(-6));

        RemoteStateBuilder.Build(Snap() with { LastLogged = logged }, Now, Ctx, "you", null).Logged.Should().BeNull();
    }
}
