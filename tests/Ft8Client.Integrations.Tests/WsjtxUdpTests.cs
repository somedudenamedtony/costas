// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Net.Sockets;
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.Integrations.Tests;

public class WsjtxUdpTests
{
    private static readonly DateTime On = new(2026, 1, 4, 3, 12, 30, DateTimeKind.Utc);

    private static QDataStreamReader Open(byte[] d, WsjtxMessageType type)
    {
        var r = new QDataStreamReader(d);
        r.UInt32().Should().Be(0xADBCCBDA);
        r.UInt32().Should().Be(2u);
        r.UInt32().Should().Be((uint)type);
        r.Utf8().Should().Be("Ft8Client");
        return r;
    }

    [Fact]
    public void Heartbeat_Read_HasSchemaAndVersion()
    {
        var r = Open(WsjtxMessages.Heartbeat("Ft8Client", "0.1.0", "abc"), WsjtxMessageType.Heartbeat);
        r.UInt32().Should().Be(3u);
        r.Utf8().Should().Be("0.1.0");
        r.Utf8().Should().Be("abc");
        r.AtEnd.Should().BeTrue();
    }

    [Fact]
    public void Heartbeat_Bytes_MatchHandWrittenHeader()
    {
        Convert.ToHexStringLower(WsjtxMessages.Heartbeat("X", "1", ""))
            .Should().Be("adbccbda" + "00000002" + "00000000" + "0000000158" + "00000003" + "0000000131" + "00000000");
    }

    [Fact]
    public void Status_Read_AllFieldsInOrder()
    {
        var s = new WsjtxStatus
        {
            DialHz = 14_074_000, Mode = "FT8", DxCall = "K1ABC", Report = "-10", TxEnabled = true, Transmitting = false, Decoding = true,
            RxDf = 1200, TxDf = 1500, DeCall = "W7LIT", DeGrid = "DN40", DxGrid = "FN42", TxWatchdog = false, TrPeriodSeconds = 15,
            ConfigurationName = "Home", TxMessage = "K1ABC W7LIT DN40",
        };
        var r = Open(WsjtxMessages.Status("Ft8Client", s), WsjtxMessageType.Status);
        r.UInt64().Should().Be(14_074_000ul);
        r.Utf8().Should().Be("FT8");
        r.Utf8().Should().Be("K1ABC");
        r.Utf8().Should().Be("-10");
        r.Utf8().Should().Be("FT8");
        r.Bool().Should().BeTrue();
        r.Bool().Should().BeFalse();
        r.Bool().Should().BeTrue();
        r.UInt32().Should().Be(1200u);
        r.UInt32().Should().Be(1500u);
        r.Utf8().Should().Be("W7LIT");
        r.Utf8().Should().Be("DN40");
        r.Utf8().Should().Be("FN42");
        r.Bool().Should().BeFalse();
        r.Utf8().Should().BeEmpty();
        r.Bool().Should().BeFalse();
        r.UInt8().Should().Be(0);
        r.UInt32().Should().Be(uint.MaxValue);
        r.UInt32().Should().Be(15u);
        r.Utf8().Should().Be("Home");
        r.Utf8().Should().Be("K1ABC W7LIT DN40");
        r.AtEnd.Should().BeTrue();
    }

    [Theory]
    [InlineData("FT8", "~")]
    [InlineData("FT4", "+")]
    public void Decode_Read_AllFields(string mode, string modeChar)
    {
        var d = new WsjtxDecode(On.AddSeconds(15), -12, 0.3, 1234, mode, "CQ K1ABC FN42", true, false);
        var r = Open(WsjtxMessages.Decode("Ft8Client", d), WsjtxMessageType.Decode);
        r.Bool().Should().BeTrue();
        r.Time().Should().Be(new TimeSpan(0, 3, 12, 45));
        r.Int32().Should().Be(-12);
        r.Double().Should().Be(0.3);
        r.UInt32().Should().Be(1234u);
        r.Utf8().Should().Be(modeChar);
        r.Utf8().Should().Be("CQ K1ABC FN42");
        r.Bool().Should().BeTrue();
        r.Bool().Should().BeFalse();
        r.AtEnd.Should().BeTrue();
    }

    [Fact]
    public void ClearAndClose_Read_HeaderOnly()
    {
        Open(WsjtxMessages.Clear("Ft8Client"), WsjtxMessageType.Clear).AtEnd.Should().BeTrue();
        Open(WsjtxMessages.Close("Ft8Client"), WsjtxMessageType.Close).AtEnd.Should().BeTrue();
    }

    [Fact]
    public void QsoLogged_Read_AllFieldsAndUtcDates()
    {
        var q = new WsjtxQso
        {
            TimeOnUtc = On, TimeOffUtc = On.AddSeconds(75.5), DxCall = "K1ABC", DxGrid = "FN42", FrequencyHz = 14_075_234, Mode = "FT8",
            ReportSent = "-10", ReportReceived = "-07", TxPower = "50", Comments = "", Name = "Al", MyCall = "W7LIT", MyGrid = "DN40",
        };
        var r = Open(WsjtxMessages.QsoLogged("Ft8Client", q), WsjtxMessageType.QsoLogged);
        r.DateTime().Should().Be(On.AddSeconds(75.5));
        r.Utf8().Should().Be("K1ABC");
        r.Utf8().Should().Be("FN42");
        r.UInt64().Should().Be(14_075_234ul);
        r.Utf8().Should().Be("FT8");
        r.Utf8().Should().Be("-10");
        r.Utf8().Should().Be("-07");
        r.Utf8().Should().Be("50");
        r.Utf8().Should().BeEmpty();
        r.Utf8().Should().Be("Al");
        r.DateTime().Should().Be(On);
        r.Utf8().Should().BeEmpty();
        r.Utf8().Should().Be("W7LIT");
        r.Utf8().Should().Be("DN40");
        r.Utf8().Should().BeEmpty();
        r.Utf8().Should().BeEmpty();
        r.Utf8().Should().BeEmpty();
        r.AtEnd.Should().BeTrue();
    }

    [Fact]
    public void LoggedAdif_Read_Text()
    {
        const string adif = "<adif_ver:5>3.1.4\n<EOH>\n<call:5>K1ABC <EOR>\n";
        var r = Open(WsjtxMessages.LoggedAdif("Ft8Client", adif), WsjtxMessageType.LoggedAdif);
        r.Utf8().Should().Be(adif);
        r.AtEnd.Should().BeTrue();
    }

    [Fact]
    public void JulianDay_KnownDates_MatchQt()
    {
        QDataStreamWriter.JulianDay(new DateTime(2000, 1, 1)).Should().Be(2451545);
        QDataStreamWriter.JulianDay(new DateTime(1970, 1, 1)).Should().Be(2440588);
    }

    [Fact]
    public async Task Send_Loopback_DatagramArrives()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        using var b = new WsjtxUdpBroadcaster("127.0.0.1", port);
        b.Send(WsjtxMessages.Clear("Ft8Client")).Should().BeTrue();
        var got = await listener.ReceiveAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Open(got.Buffer, WsjtxMessageType.Clear).AtEnd.Should().BeTrue();
        b.Sent.Should().Be(1);
    }

    [Fact]
    public void Send_NobodyListening_DoesNotThrow()
    {
        using var b = new WsjtxUdpBroadcaster("127.0.0.1", 9);
        for (var i = 0; i < 3; i++) b.Send(WsjtxMessages.Close("Ft8Client"));
    }
}
