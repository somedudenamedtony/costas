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
using Ft8Client.Core.Remote;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

public class RemoteControlTests
{
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 20, 31, 0, DateTimeKind.Utc));
    private readonly FakeRemoteTarget _target = new();
    private string _saved = string.Empty;

    private RemoteControl Make() => new(_target, _clock, new DeviceTokens(), t => _saved = t);

    [Fact]
    public async Task HandleAsync_CommandFromPhone_TakesControlAndCapsWatchdog()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("call", "ve7xyz"));

        _target.Calls.Should().Equal("call VE7XYZ");
        _target.ActivityCount.Should().Be(1);
        rc.Lease.Holder.Should().Be("phone");
        _target.WatchdogCap.Should().Be(3);
        rc.ControlFor("phone").Should().Be("you");
        rc.ControlFor("tablet").Should().Be("other");
    }

    [Fact]
    public async Task HandleAsync_Ping_DoesNotTakeControlOrCountAsInput()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("ping"));

        rc.Lease.IsHeld.Should().BeFalse();
        _target.ActivityCount.Should().Be(0);
        _target.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UnknownStation_SaysSo()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("call", "NOBODY"));
        note.Should().Be("NOBODY is not in the station list.");
    }

    [Fact]
    public async Task HandleAsync_UnknownType_SaysSoAndDoesNothing()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("transmit", "X"));
        note.Should().NotBeNull();
        _target.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("transmit")]
    [InlineData("offset")] // An empty offset box sends no hertz.
    [InlineData("call")] // No callsign.
    public async Task HandleAsync_NotUnderstood_DoesNotTakeControlOrResetWatchdog(string type)
    {
        var rc = Make();
        var note = await rc.HandleAsync("phone", new RemoteCommand(type));

        note.Should().Be("The station did not understand that.");
        rc.Lease.IsHeld.Should().BeFalse();
        _target.ActivityCount.Should().Be(0);
        _target.WatchdogCap.Should().BeNull();
    }

    [Fact]
    public void Pair_SavingFails_StillPairsForThisRun()
    {
        var rc = new RemoteControl(_target, _clock, new DeviceTokens(), _ => throw new InvalidOperationException("credential store full"));

        var token = rc.Pair();

        token.Should().NotBeNull();
        rc.SignIn(token).Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_MacroInPsk_SendsIt()
    {
        _target.Snapshot = new SessionSnapshot { Mode = Mode.Psk31 };
        var note = await Make().HandleAsync("phone", new RemoteCommand("macro", Key: 2));
        note.Should().BeNull();
        _target.Calls.Should().Equal("macro 2");
    }

    [Fact]
    public async Task HandleAsync_MacroInFt8_Refused()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("macro", Key: 2));
        note.Should().Be("Macros are for PSK and RTTY.");
        _target.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_LogNowWhenNotAllowed_Ignored()
    {
        await Make().HandleAsync("phone", new RemoteCommand("logNow"));
        _target.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task TickAsync_PhoneSilentWhileTransmitting_HaltsWithReasonAndReturnsControl()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("cq"));
        _target.WantsTransmit = true;
        _clock.Advance(TimeSpan.FromSeconds(14));
        await rc.TickAsync();
        _target.Halts.Should().BeEmpty();

        _clock.Advance(TimeSpan.FromSeconds(1));
        await rc.TickAsync();

        _target.Halts.Should().Equal("Transmit stopped at 20:31:15: lost touch with the phone.");
        rc.Lease.IsHeld.Should().BeFalse();
        _target.WatchdogCap.Should().BeNull();
    }

    [Fact]
    public async Task TickAsync_PhoneHeartbeating_KeepsTransmitting()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("cq"));
        _target.WantsTransmit = true;
        for (var i = 0; i < 12; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(5));
            await rc.HandleAsync("phone", new RemoteCommand("ping"));
            await rc.TickAsync();
        }
        _target.Halts.Should().BeEmpty();
    }

    [Fact]
    public async Task TickAsync_PhoneSilentWhileIdle_StopsWithoutAReason()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("resend"));
        _clock.Advance(TimeSpan.FromSeconds(20));
        await rc.TickAsync();
        _target.Halts.Should().Equal([null]);
        rc.Lease.IsHeld.Should().BeFalse();
    }

    [Fact]
    public async Task DeskActivity_AfterPhoneCommand_DeskControlsAndNoLinkStop()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("cq"));
        _target.WantsTransmit = true;
        rc.DeskActivity();

        _target.WatchdogCap.Should().BeNull();
        _clock.Advance(TimeSpan.FromMinutes(1));
        await rc.TickAsync();
        _target.Halts.Should().BeEmpty();
    }

    [Fact]
    public void Pair_NoCode_TokenSignsInAndIsSaved()
    {
        var rc = Make();
        var token = rc.Pair();

        token.Should().NotBeNull();
        _saved.Should().NotBeEmpty().And.NotContain(token!);
        rc.SignIn(token).Should().NotBeNull().And.Be(rc.SignIn(token));
        rc.SignIn("not-a-token").Should().BeNull();
    }
}
