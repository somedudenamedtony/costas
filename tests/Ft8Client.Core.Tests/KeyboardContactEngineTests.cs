// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Keyboard;

namespace Ft8Client.Core.Tests;

public class KeyboardContactEngineTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 22, 30, 0, DateTimeKind.Utc);
    private readonly KeyboardContactEngine _e = new("W7LIT");
    private readonly List<KeyboardLogEntry> _logged = [];

    public KeyboardContactEngineTests() => _e.Logged += _logged.Add;

    private static DateTime At(int s) => T0.AddSeconds(s);

    [Fact]
    public void CalledCq_FullContact_StepsAdvanceAndLogsOnMy73()
    {
        _e.NextMacro.Should().Be(Macro.CqKey);
        _e.Sent(Macro.CqKey, "CQ CQ CQ DE W7LIT W7LIT W7LIT DN40 K", At(0));
        _e.CallingCq.Should().BeTrue();

        _e.Heard("W7LIT DE EA8TL EA8TL KN", "EA8TL", 1180, At(20));
        _e.CallingCq.Should().BeFalse();
        _e.Partner.Should().Be("EA8TL");
        _e.NextMacro.Should().Be(Macro.ExchangeKey);
        _e.View()!.Steps.Select(s => s.State).Should().Equal(StepState.Done, StepState.Done, StepState.Now, StepState.Upcoming, StepState.Upcoming);

        _e.Sent(Macro.ExchangeKey, "EA8TL DE W7LIT GE UR 599 599 NAME TONY", At(40));
        _e.Heard("R R W7LIT DE EA8TL TNX TONY UR 579 579 NAME JUAN JUAN QTH LAS PALMAS HW? W7LIT DE EA8TL KN", "EA8TL", 1181, At(70));
        _e.NextMacro.Should().Be(Macro.SignOffKey);
        _e.Received.Should().Be(new Exchange(579, "Juan", "Las Palmas", null));

        _e.Sent(Macro.SignOffKey, "TNX JUAN 73 EA8TL DE W7LIT SK", At(90));
        var log = _logged.Should().ContainSingle().Which;
        log.Entry.Should().Be(new ContactLogEntry("EA8TL", null, 599, 579, At(0), At(90)));
        log.Name.Should().Be("Juan");
        log.Qth.Should().Be("Las Palmas");
        log.OffsetHz.Should().Be(1181);
        _e.View()!.Outcome.Should().Be(ContactOutcome.Logged);
    }

    [Fact]
    public void AnswerCq_FullContact_LogsOnTheir73()
    {
        _e.Answer("DK3RT", null, 915, "CQ CQ DE DK3RT DK3RT K", At(0));
        _e.NextMacro.Should().Be(Macro.AnswerKey);
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT W7LIT K", At(10));

        _e.Heard("W7LIT DE DK3RT GE OM TNX CALL. UR 589 589 NAME RALF RALF QTH MUNICH MUNICH. HW? W7LIT DE DK3RT K", "DK3RT", 915, At(40));
        _e.NextMacro.Should().Be(Macro.ExchangeKey);
        _e.View()!.CanLogNow.Should().BeTrue();

        _e.Sent(Macro.ExchangeKey, "DK3RT DE W7LIT R R TNX RALF UR 579 579", At(70));
        _logged.Should().BeEmpty();
        _e.Heard("TNX TONY 73 W7LIT DE DK3RT SK", "DK3RT", 916, At(95));

        var log = _logged.Should().ContainSingle().Which;
        log.Entry.ReportReceived.Should().Be(589);
        log.Entry.TimeOnUtc.Should().Be(At(10));
        log.Name.Should().Be("Ralf");
    }

    [Fact]
    public void Heard_OtherStationDuringContact_Ignored()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("W7LIT DE PY2ZZ UR 599 NAME JOAO K", "PY2ZZ", 1400, At(20));
        _e.Received.IsEmpty.Should().BeTrue();
        _e.View()!.Steps[2].State.Should().Be(StepState.Now);
    }

    [Fact]
    public void Heard_TextWithoutMyCallWhileCallingCq_StartsNothing()
    {
        _e.Sent(Macro.CqKey, "CQ", At(0));
        _e.Heard("CQ CQ DE PY2ZZ PY2ZZ K", "PY2ZZ", 1400, At(5));
        _e.Partner.Should().BeNull();
    }

    [Fact]
    public void Capture_ClickedValue_BeatsFoundOne()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("UR 589 NAME RALF K", "DK3RT", 915, At(20));
        _e.Capture(ExchangeField.Name, "RALPH");
        _e.Capture(ExchangeField.Qth, "garching bei munchen");
        _e.Received.Name.Should().Be("Ralph");
        _e.Received.Qth.Should().Be("Garching Bei Munchen");
        _e.Received.Rst.Should().Be(589);
    }

    [Fact]
    public void LogNow_MidContact_LogsWhatIsKnown()
    {
        _e.Answer("DK3RT", "JN58", 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.LogNow(At(30));
        _logged.Should().ContainSingle().Which.Entry.Grid.Should().Be("JN58");
    }

    [Fact]
    public void Abandon_InContact_EndsWithoutLogging()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Abandon();
        _e.View()!.Outcome.Should().Be(ContactOutcome.Abandoned);
        _e.InContact.Should().BeFalse();
        _logged.Should().BeEmpty();
    }

    [Fact]
    public void Watchdog_InContact_ShowsStoppedByWatchdog()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Watchdog();
        _e.View()!.Caption.Should().Be("Stopped by watchdog");
    }

    [Fact]
    public void Heard_AnswerSplitAcrossLines_StartsContactFromOverTo()
    {
        _e.Sent(Macro.CqKey, "CQ", At(0));
        _e.Heard("W7LIT DE", null, 1180, At(5));
        _e.Partner.Should().BeNull();
        _e.Heard("EA8TL EA8TL KN", "EA8TL", 1180, At(7), to: "W7LIT");
        _e.Partner.Should().Be("EA8TL");
        _e.NextMacro.Should().Be(Macro.ExchangeKey);
    }

    [Fact]
    public void Heard_PartnerWorkingAnotherStation_NotTakenAsTheirExchange()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("N0XYZ DE DK3RT UR 599 599 NAME RALF K", "DK3RT", 915, At(20)).Should().BeTrue();
        _e.Received.IsEmpty.Should().BeTrue();
        _e.Heard("UR 559 NAME OTTO K", "DK3RT", 915, At(30), to: "N0XYZ");
        _e.Received.IsEmpty.Should().BeTrue();
        _e.View()!.Steps[2].State.Should().Be(StepState.Now);
    }

    [Fact]
    public void Sent_My73AfterAnsweringCq_Logs()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("W7LIT DE DK3RT UR 589 NAME RALF K", "DK3RT", 915, At(20));
        _e.Sent(Macro.ExchangeKey, "DK3RT DE W7LIT UR 599", At(40));
        _e.Sent(Macro.SignOffKey, "TNX RALF 73 DK3RT DE W7LIT SK", At(60));
        var log = _logged.Should().ContainSingle().Which;
        log.Entry.ReportSent.Should().Be(599);
        log.Entry.ReportReceived.Should().Be(589);
    }

    [Fact]
    public void CqSent_UnfinishedContact_EndsItAndHearsNewCallers()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.CqSent(At(60));
        _e.CallingCq.Should().BeTrue();
        _e.InContact.Should().BeFalse();
        _e.Heard("W7LIT DE EA8TL EA8TL K", "EA8TL", 1400, At(80));
        _e.Partner.Should().Be("EA8TL");
        _logged.Should().BeEmpty();
    }

    [Fact]
    public void Heard_ExchangeSplitAcrossLines_Read()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("W7LIT DE DK3RT UR 589 NAME", "DK3RT", 915, At(20));
        _e.Heard("RALF QTH", "DK3RT", 915, At(22));
        _e.Heard("MUNICH. K", "DK3RT", 915, At(24));
        _e.Received.Should().Be(new Exchange(589, "Ralf", "Munich", null));
    }

    [Theory]
    [InlineData("5NN", 599)]
    [InlineData("579", 579)]
    public void Capture_Report_Normalised(string word, int rst)
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Capture(ExchangeField.Rst, word).Should().BeTrue();
        _e.Received.Rst.Should().Be(rst);
    }

    [Theory]
    [InlineData(ExchangeField.Rst, "HELLO")]
    [InlineData(ExchangeField.Grid, "MUNICH")]
    public void Capture_WordNotThatField_Refused(ExchangeField field, string word)
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Capture(field, word).Should().BeFalse();
        _e.Received.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Heard_TuInPassingAfterAnsweringCq_DoesNotLog()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("W7LIT DE DK3RT UR 589 NAME RALF K", "DK3RT", 915, At(20));
        _e.Sent(Macro.ExchangeKey, "DK3RT DE W7LIT UR 599", At(40));
        _e.Heard("R R TU FER RPT. RIG IS FT991 HW?", "DK3RT", 915, At(60));
        _logged.Should().BeEmpty();
        _e.InContact.Should().BeTrue();
    }

    [Fact]
    public void Heard_NoSenderNearPartnerNotNamingMe_NotTheirs()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("UR 599 NAME JOAO K", null, 940, At(20)).Should().BeFalse();
        _e.Received.IsEmpty.Should().BeTrue();
        _e.Heard("W7LIT UR 589 K", null, 920, At(30)).Should().BeTrue();
        _e.Received.Rst.Should().Be(589);
    }

    [Fact]
    public void LogNow_AfterAbandon_NotLogged()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Heard("W7LIT DE DK3RT UR 589 K", "DK3RT", 915, At(20));
        _e.Abandon();
        _e.View()!.CanLogNow.Should().BeFalse();
        _e.LogNow(At(30));
        _logged.Should().BeEmpty();
    }

    [Fact]
    public void LogNow_BeforeMyReportWent_ReportSentNotLogged()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.LogNow(At(30));
        _logged.Should().ContainSingle().Which.Entry.ReportSent.Should().BeNull();
    }

    [Fact]
    public void Heard_PartnerWithPortableSuffix_SameStation()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.Sent(Macro.AnswerKey, "DK3RT DE W7LIT K", At(10));
        _e.Heard("W7LIT/P DE DK3RT/P UR 579 K", "DK3RT/P", 915, At(20)).Should().BeTrue();
        _e.Received.Rst.Should().Be(579);
        _e.NextMacro.Should().Be(Macro.ExchangeKey);
    }

    [Fact]
    public void Heard_TailEnderAfterLog_StartsNewContact()
    {
        _e.Sent(Macro.CqKey, "CQ", At(0));
        _e.Heard("W7LIT DE EA8TL EA8TL K", "EA8TL", 1180, At(10));
        _e.Sent(Macro.ExchangeKey, "EA8TL DE W7LIT UR 599", At(20));
        _e.Heard("W7LIT DE EA8TL UR 579 K", "EA8TL", 1180, At(40));
        _e.Sent(Macro.SignOffKey, "73 EA8TL DE W7LIT SK", At(60));
        _logged.Should().ContainSingle();

        _e.Heard("73 W7LIT DE EA8TL SK", "EA8TL", 1180, At(70));
        _e.Partner.Should().Be("EA8TL");
        _e.InContact.Should().BeFalse();

        _e.Heard("W7LIT DE N0XYZ N0XYZ K", "N0XYZ", 1300, At(80));
        _e.Partner.Should().Be("N0XYZ");
        _e.InContact.Should().BeTrue();
        _e.NextMacro.Should().Be(Macro.ExchangeKey);
    }

    [Fact]
    public void Heard_CallerLongAfterLog_Ignored()
    {
        _e.Answer("DK3RT", null, 915, null, At(0));
        _e.LogNow(At(10));
        _e.Heard("W7LIT DE N0XYZ N0XYZ K", "N0XYZ", 1300, At(10 + 600));
        _e.Partner.Should().Be("DK3RT");
    }

    [Fact]
    public void Answer_WhileCallingCq_StopsCalling()
    {
        var e = new KeyboardContactEngine("W7LIT");
        e.CqSent(At(0));
        e.Answer("KP4JRS", null, 1180, "CQ DE KP4JRS K", At(5));
        e.CallingCq.Should().BeFalse();
        e.Partner.Should().Be("KP4JRS");
    }
}
