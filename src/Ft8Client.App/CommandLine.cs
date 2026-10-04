// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App;

/// <summary>Command-line options.</summary>
public sealed record CommandLine
{
    /// <summary>Replay WAV files from this folder instead of the sound card (forces simulation).</summary>
    public string? SimulateFolder { get; init; }

    /// <summary>Add the scripted partner; optional file of partner calls.</summary>
    public string? PartnerFile { get; init; }

    /// <summary>True when --simulate-partner was given.</summary>
    public bool SimulatePartner { get; init; }

    /// <summary>Use a different data folder (portable, testing).</summary>
    public string? Home { get; init; }

    /// <summary>Parses arguments. Unknown arguments are ignored.</summary>
    public static CommandLine Parse(string[] args)
    {
        var c = new CommandLine();
        for (var i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
            switch (args[i])
            {
                case "--simulate": c = c with { SimulateFolder = Next() ?? "samples" }; break;
                case "--simulate-partner": c = c with { SimulatePartner = true, PartnerFile = Next() }; break;
                case "--home": c = c with { Home = Next() }; break;
            }
        }
        return c;
    }
}
