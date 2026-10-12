// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Net.Sockets;
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.Integrations.Tests;

public class WsjtxInboundTests
{
    private static QDataStreamWriter Header(uint type, string id = "Costas", uint schema = 2) =>
        new QDataStreamWriter().UInt32(0xADBCCBDA).UInt32(schema).UInt32(type).Utf8(id);

    // QColor: qint8 spec, then alpha, red, green, blue, pad as quint16 (8-bit values repeated in both bytes).
    private static QDataStreamWriter Color(QDataStreamWriter w, byte spec, byte a, byte r, byte g, byte b)
    {
        w.UInt8(spec);
        foreach (var v in new[] { a, r, g, b, (byte)0 }) w.UInt8(v).UInt8(v);
        return w;
    }

    [Fact]
    public void Parse_Reply_AllFields()
    {
        var d = Header(4).UInt32(((2 * 60 + 54) * 60 + 15) * 1000).Int32(-12).Double(0.3).UInt32(1234).Utf8("~").Utf8("CQ K1ABC FN42")
            .Bool(false).UInt8(0x02).ToArray();

        var m = WsjtxInbound.Parse(d).Should().BeOfType<WsjtxReply>().Subject;

        m.TargetId.Should().Be("Costas");
        m.TimeOfDay.Should().Be(new TimeSpan(2, 54, 15));
        m.Snr.Should().Be(-12);
        m.Dt.Should().Be(0.3);
        m.OffsetHz.Should().Be(1234u);
        m.Mode.Should().Be("~");
        m.Message.Should().Be("CQ K1ABC FN42");
        m.LowConfidence.Should().BeFalse();
        m.Modifiers.Should().Be(0x02);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parse_HaltTx_ReadsAutoTxOnly(bool autoOnly)
    {
        WsjtxInbound.Parse(Header(8).Bool(autoOnly).ToArray()).Should().Be(new WsjtxHaltTx("Costas", autoOnly));
    }

    [Fact]
    public void Parse_Highlight_RgbColours()
    {
        var w = Header(13).Utf8("K1ABC");
        Color(w, 1, 255, 255, 255, 0);
        Color(w, 1, 255, 0, 0, 0);
        var m = WsjtxInbound.Parse(w.Bool(true).ToArray()).Should().BeOfType<WsjtxHighlight>().Subject;

        m.Callsign.Should().Be("K1ABC");
        m.Background.Should().Be(new WsjtxColor(255, 255, 255, 0));
        m.Foreground.Should().Be(new WsjtxColor(255, 0, 0, 0));
        m.LastOnly.Should().BeTrue();
        m.Clears.Should().BeFalse();
    }

    [Fact]
    public void Parse_HighlightInvalidColour_Clears()
    {
        var w = Header(13).Utf8("K1ABC");
        Color(w, 0, 0, 0, 0, 0);
        Color(w, 0, 0, 0, 0, 0);
        WsjtxInbound.Parse(w.Bool(false).ToArray()).Should().BeOfType<WsjtxHighlight>().Which.Clears.Should().BeTrue();
    }

    [Fact]
    public void Parse_TruncatedOrForeign_ReturnsNull()
    {
        var reply = Header(4).UInt32(0).Int32(-12).ToArray();
        WsjtxInbound.Parse(reply).Should().BeNull();
        WsjtxInbound.Parse([1, 2, 3]).Should().BeNull();
        WsjtxInbound.Parse(Header(8, schema: 9).Bool(false).ToArray()).Should().BeNull();
    }

    [Fact]
    public void Parse_TypesThisAppIgnores_ReturnsNull()
    {
        WsjtxInbound.Parse(Header(9).Utf8("TNX 73").Bool(true).ToArray()).Should().BeNull(); // Free text
        WsjtxInbound.Parse(Header(7).ToArray()).Should().BeNull(); // Replay
    }

    [Fact]
    public async Task Received_DatagramSentBackToOurSocket_Raised()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        using var b = new WsjtxUdpBroadcaster("127.0.0.1", port);
        var got = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        b.Received += (d, _) => got.TrySetResult(d);

        b.Send(WsjtxMessages.Clear("Costas"));
        var first = await listener.ReceiveAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var halt = Header(8).Bool(false).ToArray();
        await listener.SendAsync(halt, first.RemoteEndPoint, TestContext.Current.CancellationToken);

        (await got.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().Equal(halt);
    }
}
