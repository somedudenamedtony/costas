// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.Core.Remote;

namespace Ft8Client.Core.Tests;

public class RemoteControlRulesTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LinkLost_HolderSilentForOneSlot_True()
    {
        var lease = new ControlLease();
        lease.Take("phone", T0);
        lease.LinkLost(T0.AddSeconds(14.9)).Should().BeFalse();
        lease.LinkLost(T0.AddSeconds(15)).Should().BeTrue();
    }

    [Fact]
    public void LinkLost_HolderHeartbeats_False()
    {
        var lease = new ControlLease();
        lease.Take("phone", T0);
        lease.Heartbeat("phone", T0.AddSeconds(10));
        lease.LinkLost(T0.AddSeconds(20)).Should().BeFalse();
    }

    [Fact]
    public void LinkLost_OtherDeviceHeartbeats_StillLost()
    {
        var lease = new ControlLease();
        lease.Take("phone", T0);
        lease.Heartbeat("tablet", T0.AddSeconds(10));
        lease.LinkLost(T0.AddSeconds(15)).Should().BeTrue();
    }

    [Fact]
    public void LinkLost_DeskHasControl_False()
    {
        var lease = new ControlLease();
        lease.LinkLost(T0.AddHours(1)).Should().BeFalse();
        lease.Take("phone", T0);
        lease.Release();
        lease.LinkLost(T0.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void WatchdogMinutes_PhoneHasControl_CappedAtThree()
    {
        var lease = new ControlLease();
        lease.WatchdogMinutes(6).Should().Be(6);
        lease.Take("phone", T0);
        lease.WatchdogMinutes(6).Should().Be(3);
        lease.WatchdogMinutes(2).Should().Be(2);
    }

    [Fact]
    public void TryRedeem_RightCodeInTime_TrueOnce()
    {
        var gate = new PairingGate(() => 42);
        gate.Open(T0).Should().Be("000042");
        gate.TryRedeem("000042", T0.AddMinutes(9)).Should().BeTrue();
        gate.TryRedeem("000042", T0.AddMinutes(9)).Should().BeFalse();
    }

    [Fact]
    public void TryRedeem_Expired_False()
    {
        var gate = new PairingGate(() => 123456);
        gate.Open(T0);
        gate.TryRedeem("123456", T0.AddMinutes(10)).Should().BeFalse();
        gate.Code.Should().BeNull();
    }

    [Fact]
    public void TryRedeem_FiveWrongCodes_CodeVoid()
    {
        var gate = new PairingGate(() => 123456);
        gate.Open(T0);
        for (var i = 0; i < PairingGate.MaxAttempts; i++) gate.TryRedeem("000000", T0).Should().BeFalse();
        gate.TryRedeem("123456", T0).Should().BeFalse();
    }

    [Fact]
    public void TryRedeem_NoCodeOpen_False()
    {
        new PairingGate(() => 1).TryRedeem("000001", T0).Should().BeFalse();
    }

    [Fact]
    public void Verify_IssuedTokenAfterRoundTrip_True()
    {
        var tokens = new DeviceTokens();
        var token = tokens.Issue();
        var saved = tokens.Serialize();
        saved.Should().NotContain(token);
        var loaded = DeviceTokens.Parse(saved);
        loaded.Verify(token).Should().BeTrue();
        loaded.Verify("nope").Should().BeFalse();
        loaded.Verify(null).Should().BeFalse();
    }

    [Fact]
    public void Clear_AfterIssue_TokenRefused()
    {
        var tokens = new DeviceTokens();
        var token = tokens.Issue();
        tokens.Clear();
        tokens.Verify(token).Should().BeFalse();
        tokens.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_Garbage_Empty()
    {
        DeviceTokens.Parse("hello,world").Count.Should().Be(0);
        DeviceTokens.Parse(null).Count.Should().Be(0);
    }

    [Theory]
    [InlineData("192.168.1.20", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.101.102.103", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("169.254.3.4", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fd12:3456::1", true)]
    [InlineData("2001:4860::8888", false)]
    [InlineData("::ffff:192.168.1.9", true)]
    [InlineData("::ffff:8.8.4.4", false)]
    public void Contains_Address_OnlyHomeNetworks(string address, bool expected)
    {
        LocalNetwork.Contains(IPAddress.Parse(address)).Should().Be(expected);
    }

    [Fact]
    public void Issue_PastTheCap_ForgetsTheOldestAndStaysUnderTheCredentialLimit()
    {
        var tokens = new DeviceTokens();
        var first = tokens.Issue();
        var rest = Enumerable.Range(0, DeviceTokens.MaxDevices).Select(_ => tokens.Issue()).ToList();

        tokens.Count.Should().Be(DeviceTokens.MaxDevices);
        tokens.Verify(first).Should().BeFalse();
        rest.Should().OnlyContain(t => tokens.Verify(t));
        // Windows stores the list as UTF-16 and refuses more than 2,560 bytes.
        (tokens.Serialize().Length * 2).Should().BeLessThan(2560);
        DeviceTokens.Parse(tokens.Serialize() + "," + tokens.Serialize()).Count.Should().Be(DeviceTokens.MaxDevices);
    }
}
