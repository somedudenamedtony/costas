// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Net.NetworkInformation;
using Sentry.Extensibility;

namespace Ft8Client.Integrations.Crash;

/// <summary>
/// Tells Sentry's offline cache whether the computer is online, so that while it is offline nothing is sent or tried:
/// reports wait in the cache and go out once the network is back.
/// </summary>
internal sealed class NetworkStatus(Func<bool> online) : INetworkStatusListener
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(30);

    public bool Online
    {
        get
        {
            try
            {
                return online();
            }
            catch (NetworkInformationException)
            {
                return false;
            }
        }
    }

    public async Task WaitForNetworkOnlineAsync(CancellationToken cancellationToken = default)
    {
        while (!Online) await Task.Delay(Poll, cancellationToken).ConfigureAwait(false);
    }
}
