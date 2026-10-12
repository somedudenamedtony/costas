// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Security.Cryptography;

namespace Ft8Client.Core.Remote;

/// <summary>
/// The paired devices. Each holds a random token; only SHA-256 hashes are kept here, so the stored list cannot be used to
/// sign in. Serialized to one line for the OS credential store.
/// </summary>
public sealed class DeviceTokens
{
    private readonly object _gate = new();
    private readonly List<string> _hashes = [];

    /// <summary>Number of paired devices.</summary>
    public int Count
    {
        get
        {
            lock (_gate) return _hashes.Count;
        }
    }

    /// <summary>Loads hashes saved by <see cref="Serialize"/>; anything that is not a hash is dropped.</summary>
    public static DeviceTokens Parse(string? saved)
    {
        var t = new DeviceTokens();
        foreach (var h in (saved ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (h.Length == 64 && h.All(Uri.IsHexDigit)) t._hashes.Add(h.ToUpperInvariant());
        }
        return t;
    }

    /// <summary>The hashes, comma separated.</summary>
    public string Serialize()
    {
        lock (_gate) return string.Join(',', _hashes);
    }

    /// <summary>Makes a new token for a device and remembers its hash. The token is shown to the device once.</summary>
    public string Issue()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (_gate) _hashes.Add(Hash(token));
        return token;
    }

    /// <summary>True when <paramref name="token"/> belongs to a paired device.</summary>
    public bool Verify(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 128) return false;
        var h = System.Text.Encoding.ASCII.GetBytes(Hash(token));
        lock (_gate)
        {
            var ok = false;
            foreach (var x in _hashes) ok |= CryptographicOperations.FixedTimeEquals(h, System.Text.Encoding.ASCII.GetBytes(x));
            return ok;
        }
    }

    /// <summary>Forgets every paired device.</summary>
    public void Clear()
    {
        lock (_gate) _hashes.Clear();
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
