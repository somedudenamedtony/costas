// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Lotw;

/// <summary>How a TQSL run ended.</summary>
/// <param name="Code">TQSL's exit code (docs/05-integrations.md), or -1 when it could not be run.</param>
/// <param name="Message">What to tell the operator.</param>
public sealed record TqslResult(int Code, string Message)
{
    /// <summary>
    /// True when LoTW has the contacts now: everything uploaded (0), or the rest were duplicates LoTW already had (8, 9).
    /// </summary>
    public bool Uploaded => Code is 0 or 8 or 9;

    /// <summary>What a TQSL exit code means.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Uploaded to LoTW",
        1 => "TQSL was cancelled",
        2 => "LoTW rejected the upload",
        3 => "LoTW gave an unexpected answer",
        4 => "TQSL reported an error (check the callsign certificate and Station Location)",
        5 => "TQSL's signing library reported an error",
        6 => "TQSL could not open the log file",
        7 => "TQSL could not write its output file",
        8 => "LoTW already had all of these contacts",
        9 => "Uploaded; LoTW already had some of them",
        10 => "TQSL did not understand the command (check the Station Location name)",
        11 => "LoTW unreachable",
        _ => $"TQSL ended with code {code}",
    };
}
