// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;

namespace Ft8Client.Core.Keyboard;

/// <summary>
/// Follows the text of one keyboard-mode channel and works out whose it is (docs/09-keyboard-modes.md section 6). The
/// channel keeps its station until a different sender is seen or it has been quiet for <see cref="QuietAfter"/>.
/// </summary>
public sealed class TextChannel
{
    /// <summary>Characters kept for "Doing now".</summary>
    public const int RecentLength = 60;

    /// <summary>Quiet time after which the channel forgets its station.</summary>
    public static readonly TimeSpan QuietAfter = TimeSpan.FromSeconds(60);

    // The current over is cut at this length, so text that never says K does not grow without end.
    private const int OverLength = 1000;

    private readonly StringBuilder _over = new();
    private string _recent = string.Empty;
    private DateTime _lastUtc;
    private string? _to;

    /// <summary>The station this channel's text belongs to, once named.</summary>
    public string? Call { get; private set; }

    /// <summary>The channel's last <see cref="RecentLength"/> characters.</summary>
    public string RecentText => _recent;

    /// <summary>Adds text read on the channel; returns who sent it, or null while no sender is known.</summary>
    /// <param name="text">The new characters.</param>
    /// <param name="utc">When they arrived.</param>
    /// <param name="offsetHz">The channel's audio frequency.</param>
    /// <param name="snr">Its SNR in dB in 2,500 Hz.</param>
    public TextHeard? Add(string text, DateTime utc, int offsetHz, int snr)
    {
        if (Call is not null && utc - _lastUtc > QuietAfter) (Call, _to) = (null, null);
        _lastUtc = utc;
        _over.Append(text);
        if (_over.Length > OverLength) _over.Remove(0, _over.Length - OverLength);
        var recent = (_recent + text).Replace('\n', ' ').Replace('\r', ' ');
        _recent = recent.Length > RecentLength ? recent[^RecentLength..] : recent;

        var over = OverReader.Read(_over.ToString());
        if (over.Sender is { } sender)
        {
            if (sender != Call) _to = null;
            Call = sender;
            _to = over.To ?? _to;
        }
        if (over.Ended) _over.Clear();
        return Call is null ? null : new TextHeard(Call, _to, over.Cq, over.Ended, _recent, utc, offsetHz, snr);
    }
}
