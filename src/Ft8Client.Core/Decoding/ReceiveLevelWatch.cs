// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>
/// Watches the receive audio while Costas runs, the way Setup's input check does once, and reports a problem only after
/// it has lasted <see cref="WarnAfterSeconds"/> seconds of receiving, so a burst of noise or a short pause never
/// raises it. Seconds spent transmitting (when the radio mutes its receive audio) are not counted. Only clear faults are
/// reported (no audio, silence, clipping): a quiet band is not a fault. Pure; fed once a second.
/// </summary>
public sealed class ReceiveLevelWatch
{
    /// <summary>Seconds of receiving with the same problem before it is reported.</summary>
    public const int WarnAfterSeconds = 30;

    /// <summary>Seconds of good audio before a reported problem is cleared.</summary>
    public const int ClearAfterSeconds = 5;

    /// <summary>A peak at or above this, in dBFS, counts as clipping.</summary>
    public const double ClipDbfs = -1;

    private ReceiveLevelProblem _seen;
    private int _seenFor;
    private int _goodFor;

    /// <summary>The problem being reported, or <see cref="ReceiveLevelProblem.None"/>.</summary>
    public ReceiveLevelProblem Problem { get; private set; }

    /// <summary>Takes one second of measurement. Returns true when <see cref="Problem"/> changed.</summary>
    /// <param name="samples">Input samples received during the second.</param>
    /// <param name="peakDbfs">The largest sample in the second, dBFS (-120 for all zero).</param>
    /// <param name="transmitting">The radio was transmitting (or just stopped): the second is not counted.</param>
    public bool Update(long samples, double peakDbfs, bool transmitting)
    {
        if (transmitting) return false;
        var now = samples <= 0 ? ReceiveLevelProblem.NoAudio
            : peakDbfs <= -120 ? ReceiveLevelProblem.Silence
            : peakDbfs >= ClipDbfs ? ReceiveLevelProblem.Clipping
            : ReceiveLevelProblem.None;
        var before = Problem;
        if (now == ReceiveLevelProblem.None)
        {
            _seen = ReceiveLevelProblem.None;
            _seenFor = 0;
            if (Problem != ReceiveLevelProblem.None && ++_goodFor >= ClearAfterSeconds) Problem = ReceiveLevelProblem.None;
            return Problem != before;
        }
        _goodFor = 0;
        _seenFor = now == _seen ? _seenFor + 1 : 1;
        _seen = now;
        if (_seenFor >= WarnAfterSeconds) Problem = now;
        return Problem != before;
    }

    /// <summary>The status line text for a problem, or null for none.</summary>
    public static string? Text(ReceiveLevelProblem p) => p switch
    {
        ReceiveLevelProblem.NoAudio => "No audio is arriving from the radio's sound device. It may be unplugged or asleep, or another program may be holding it.",
        ReceiveLevelProblem.Silence => "The radio's sound device sends only silence. Check that the radio's USB audio output is turned up and not muted.",
        ReceiveLevelProblem.Clipping => "The receive audio is clipping. Turn down the radio's USB audio output level or the computer's recording level.",
        _ => null,
    };
}
