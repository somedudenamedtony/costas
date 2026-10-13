// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Services;

/// <summary>
/// Decides whether an unexpected error on the UI thread (a button's command failing on a driver or system error) is
/// shown and survived, or ends the app. One error is survived so the operator can carry on; errors repeating
/// quickly mean the app is stuck in a broken state, and those end it as before.
/// </summary>
public sealed class UnexpectedErrorPolicy
{
    private readonly Queue<DateTime> _recent = new();
    private readonly int _limit;
    private readonly TimeSpan _window;

    /// <summary>Creates the policy: at most <paramref name="limit"/> errors survived within <paramref name="window"/>.</summary>
    public UnexpectedErrorPolicy(int limit = 3, TimeSpan? window = null)
    {
        _limit = limit;
        _window = window ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Records an error at <paramref name="nowUtc"/>; true when the app should carry on.</summary>
    public bool Survive(DateTime nowUtc)
    {
        lock (_recent)
        {
            while (_recent.Count > 0 && nowUtc - _recent.Peek() > _window) _recent.Dequeue();
            _recent.Enqueue(nowUtc);
            return _recent.Count <= _limit;
        }
    }

    /// <summary>The line shown to the operator.</summary>
    public static string Describe(Exception ex) =>
        $"Something went wrong: {ex.Message} Transmit is stopped as a precaution. Details are in the log (☰ › Diagnostics).";
}
