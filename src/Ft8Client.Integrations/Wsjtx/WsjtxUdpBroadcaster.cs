// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Net.Sockets;

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>
/// Sends WSJT-X UDP datagrams to one address (unicast or multicast) from one socket. Inbound datagrams are read and
/// ignored (v1 does not act on them). Send failures never throw: interop is optional.
/// </summary>
public sealed class WsjtxUdpBroadcaster : IDisposable
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _target;
    private readonly CancellationTokenSource _cts = new();
    private int _sent;

    /// <summary>Creates a broadcaster. Throws <see cref="FormatException"/> for an address that is not an IP address.</summary>
    public WsjtxUdpBroadcaster(string address, int port)
    {
        var ip = IPAddress.Parse(address.Trim());
        _target = new IPEndPoint(ip, port);
        _udp = new UdpClient(ip.AddressFamily);
        if (OperatingSystem.IsWindows())
        {
            // Stop ICMP "port unreachable" (nobody listening yet) from surfacing as receive errors.
            const int SioUdpConnreset = -1744830452;
            _udp.Client.IOControl(SioUdpConnreset, [0, 0, 0, 0], null);
        }
        if (IsMulticast(ip))
        {
            _udp.Client.SetSocketOption(ip.AddressFamily == AddressFamily.InterNetworkV6 ? SocketOptionLevel.IPv6 : SocketOptionLevel.IP,
                SocketOptionName.MulticastTimeToLive, 1);
            _udp.MulticastLoopback = true;
        }
        _udp.Client.Bind(new IPEndPoint(ip.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
        _ = Task.Run(DrainAsync);
    }

    /// <summary>Datagrams sent.</summary>
    public int Sent => Volatile.Read(ref _sent);

    /// <summary>The last send error, if the last send failed.</summary>
    public string? LastError { get; private set; }

    /// <summary>Sends one datagram. Returns false (and records the error) on failure.</summary>
    public bool Send(byte[] datagram)
    {
        try
        {
            _udp.Send(datagram, datagram.Length, _target);
            Interlocked.Increment(ref _sent);
            LastError = null;
            return true;
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private static bool IsMulticast(IPAddress ip) =>
        ip.AddressFamily == AddressFamily.InterNetworkV6 ? ip.IsIPv6Multicast : (ip.GetAddressBytes()[0] & 0xF0) == 0xE0;

    private async Task DrainAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await _udp.ReceiveAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _udp.Dispose();
        _cts.Dispose();
    }
}
