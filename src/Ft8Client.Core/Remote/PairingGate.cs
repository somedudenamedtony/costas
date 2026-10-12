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
/// The short code the operator types on a phone to pair it. One code at a time, valid for <see cref="Lifetime"/>, used once,
/// and void after <see cref="MaxAttempts"/> wrong guesses, so it cannot be guessed from the network.
/// </summary>
public sealed class PairingGate
{
    /// <summary>How long a code stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Wrong codes allowed before the code is void.</summary>
    public const int MaxAttempts = 5;

    private readonly Func<int> _random;
    private readonly object _gate = new();
    private string? _code;
    private DateTime _expiresUtc;
    private int _failures;

    /// <summary>Creates the gate with a cryptographic random source.</summary>
    public PairingGate()
        : this(() => RandomNumberGenerator.GetInt32(0, 1_000_000))
    {
    }

    /// <summary>Creates the gate with the given source of numbers from 0 to 999999 (for tests).</summary>
    public PairingGate(Func<int> random) => _random = random;

    /// <summary>The current code, or null when none is open.</summary>
    public string? Code
    {
        get
        {
            lock (_gate) return _code;
        }
    }

    /// <summary>When the current code expires.</summary>
    public DateTime ExpiresUtc
    {
        get
        {
            lock (_gate) return _expiresUtc;
        }
    }

    /// <summary>Opens a new six-digit code, replacing any earlier one.</summary>
    public string Open(DateTime nowUtc)
    {
        lock (_gate)
        {
            _code = _random().ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            _expiresUtc = nowUtc + Lifetime;
            _failures = 0;
            return _code;
        }
    }

    /// <summary>Closes the current code.</summary>
    public void Close()
    {
        lock (_gate) _code = null;
    }

    /// <summary>True, and the code is closed, when <paramref name="code"/> matches the open code in time.</summary>
    public bool TryRedeem(string? code, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (_code is null) return false;
            if (nowUtc >= _expiresUtc)
            {
                _code = null;
                return false;
            }
            var given = System.Text.Encoding.ASCII.GetBytes((code ?? string.Empty).Trim());
            var want = System.Text.Encoding.ASCII.GetBytes(_code);
            if (CryptographicOperations.FixedTimeEquals(given, want))
            {
                _code = null;
                return true;
            }
            if (++_failures >= MaxAttempts) _code = null;
            return false;
        }
    }
}
