// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Ft8Client.Integrations.Time;

/// <summary>
/// SNTP (RFC 4330) over UDP port 123. Takes 4 samples and keeps the one with the lowest round-trip delay.
/// Offline is not an error: returns null. The app never sets the system clock.
/// </summary>
public sealed class SntpClient(string server = "pool.ntp.org", int port = 123)
{
    private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Measures the offset, or returns null when no server answered.</summary>
    public async Task<SntpResult?> MeasureAsync(CancellationToken ct, int samples = 4)
    {
        SntpResult? best = null;
        for (var i = 0; i < samples; i++)
        {
            try
            {
                var r = await SampleAsync(ct).ConfigureAwait(false);
                if (best is null || r.RoundTrip < best.RoundTrip) best = r;
            }
            catch (SocketException) { }
            catch (TimeoutException) { }
        }
        return best;
    }

    /// <summary>Computes offset and delay from the four NTP timestamps (t1 send, t2 server receive, t3 server send, t4 receive).</summary>
    public static (TimeSpan Offset, TimeSpan Delay) Compute(DateTime t1, DateTime t2, DateTime t3, DateTime t4) =>
        (TimeSpan.FromTicks(((t2 - t1) + (t3 - t4)).Ticks / 2), (t4 - t1) - (t3 - t2));

    /// <summary>Reads an NTP 64-bit timestamp.</summary>
    public static DateTime ReadTimestamp(ReadOnlySpan<byte> b)
    {
        var sec = BinaryPrimitives.ReadUInt32BigEndian(b);
        var frac = BinaryPrimitives.ReadUInt32BigEndian(b[4..]);
        return NtpEpoch.AddTicks(sec * TimeSpan.TicksPerSecond + (long)(frac * (double)TimeSpan.TicksPerSecond / 4294967296.0));
    }

    /// <summary>Writes an NTP 64-bit timestamp.</summary>
    public static void WriteTimestamp(Span<byte> b, DateTime t)
    {
        var ticks = (t - NtpEpoch).Ticks;
        BinaryPrimitives.WriteUInt32BigEndian(b, (uint)(ticks / TimeSpan.TicksPerSecond));
        BinaryPrimitives.WriteUInt32BigEndian(b[4..], (uint)(ticks % TimeSpan.TicksPerSecond * 4294967296.0 / TimeSpan.TicksPerSecond));
    }

    private async Task<SntpResult> SampleAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.Connect(server, port);
        var req = new byte[48];
        req[0] = 0x1B; // LI 0, version 3, mode 3 (client)
        var sw = Stopwatch.StartNew();
        var t1 = DateTime.UtcNow;
        WriteTimestamp(req.AsSpan(40), t1);
        await udp.SendAsync(req, ct).ConfigureAwait(false);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        UdpReceiveResult res;
        try
        {
            res = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
        var t4 = t1 + sw.Elapsed;
        if (res.Buffer.Length < 48) throw new TimeoutException();
        var t2 = ReadTimestamp(res.Buffer.AsSpan(32));
        var t3 = ReadTimestamp(res.Buffer.AsSpan(40));
        var (offset, delay) = Compute(t1, t2, t3, t4);
        return new SntpResult(offset, delay, server);
    }
}
