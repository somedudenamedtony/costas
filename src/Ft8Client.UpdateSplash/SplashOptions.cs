// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.UpdateSplash;

/// <summary>
/// What Costas tells the splash on its command line: which process to wait for, the version being installed, where to
/// sit and the colours of the theme in use. Also compiled into the app (linked), which builds the command line with
/// <see cref="ToArguments"/>.
/// </summary>
public sealed record SplashOptions
{
    /// <summary>The Costas that is closing for the update; 0 when unknown.</summary>
    public int ProcessId { get; init; }

    /// <summary>The version being installed, as shown ("0.1.312"); empty when unknown.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>The centre of Costas's window in screen pixels, so the splash appears where the app was; null centres it on the main screen.</summary>
    public (int X, int Y)? Center { get; init; }

    /// <summary>Card background, as 0xRRGGBB.</summary>
    public int Background { get; init; } = 0x2B2B2B;

    /// <summary>Card border, as 0xRRGGBB.</summary>
    public int Border { get; init; } = 0x3A3A3A;

    /// <summary>Title text, as 0xRRGGBB.</summary>
    public int Text { get; init; } = 0xF2F2F2;

    /// <summary>Status text, as 0xRRGGBB.</summary>
    public int SecondaryText { get; init; } = 0xAAAAAA;

    /// <summary>The moving progress bar, as 0xRRGGBB.</summary>
    public int Accent { get; init; } = 0x0078D4;

    /// <summary>The command line for these options.</summary>
    public IReadOnlyList<string> ToArguments()
    {
        var args = new List<string> { "--pid", ProcessId.ToString(CultureInfo.InvariantCulture) };
        if (Version.Length > 0) args.AddRange(["--version", Version]);
        if (Center is { } c) args.AddRange(["--center", string.Create(CultureInfo.InvariantCulture, $"{c.X},{c.Y}")]);
        args.AddRange(["--background", Hex(Background), "--border", Hex(Border), "--text", Hex(Text), "--secondary", Hex(SecondaryText), "--accent", Hex(Accent)]);
        return args;
    }

    /// <summary>
    /// Reads a command line made by <see cref="ToArguments"/>. Anything it does not understand keeps its default, so a
    /// newer Costas starting an older splash (or the other way round) still gets a splash.
    /// </summary>
    public static SplashOptions Parse(IReadOnlyList<string> args)
    {
        var o = new SplashOptions();
        for (var i = 0; i + 1 < args.Count; i++)
        {
            var value = args[i + 1];
            switch (args[i])
            {
                case "--pid" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid):
                    o = o with { ProcessId = pid };
                    break;
                case "--version":
                    o = o with { Version = value.Length <= 40 ? value : value[..40] };
                    break;
                case "--center" when ParsePoint(value) is { } p:
                    o = o with { Center = p };
                    break;
                case "--background" when ParseHex(value) is { } c:
                    o = o with { Background = c };
                    break;
                case "--border" when ParseHex(value) is { } c:
                    o = o with { Border = c };
                    break;
                case "--text" when ParseHex(value) is { } c:
                    o = o with { Text = c };
                    break;
                case "--secondary" when ParseHex(value) is { } c:
                    o = o with { SecondaryText = c };
                    break;
                case "--accent" when ParseHex(value) is { } c:
                    o = o with { Accent = c };
                    break;
                default:
                    continue;
            }
            i++;
        }
        return o;
    }

    private static string Hex(int rgb) => (rgb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

    private static int? ParseHex(string s)
    {
        if (s.StartsWith('#')) s = s[1..];
        return s.Length == 6 && int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static (int, int)? ParsePoint(string s)
    {
        var parts = s.Split(',');
        return parts.Length == 2 &&
               int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) &&
               int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y)
            ? (x, y)
            : null;
    }
}
