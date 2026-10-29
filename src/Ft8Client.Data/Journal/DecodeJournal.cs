// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text;

namespace Ft8Client.Data.Journal;

/// <summary>
/// The decode journal: plain text, one line per decode and per transmission, one file per month
/// (<c>decodes-YYYY-MM.txt</c>), in the spirit of WSJT-X <c>ALL.TXT</c>.
/// </summary>
public sealed class DecodeJournal(string folder)
{
    private readonly Lock _gate = new();

    /// <summary>The file a time belongs in.</summary>
    public string PathFor(DateTime utc) => Path.Combine(folder, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"decodes-{utc:yyyy-MM}.txt"));

    /// <summary>Formats one line: <c>260104_025345    14.074 Rx FT8     -8  0.1 1234 CQ JA1QRS PM95</c>.</summary>
    public static string Format(DateTime utc, long dialHz, bool transmit, string mode, int snr, double dt, int offsetHz, string message) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{utc:yyMMdd_HHmmss} {dialHz / 1e6,9:F3} {(transmit ? "Tx" : "Rx")} {mode,-3}{snr,7} {dt,4:F1} {offsetHz,4} {message}");

    /// <summary>Appends lines to the month file of <paramref name="utc"/>. I/O errors propagate.</summary>
    public void Append(DateTime utc, IEnumerable<string> lines)
    {
        var text = new StringBuilder();
        foreach (var l in lines) text.Append(l).Append('\n');
        if (text.Length == 0) return;
        lock (_gate)
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(PathFor(utc), text.ToString(), new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// Streams every line of every month file, oldest month first, without loading whole files. Files are opened so
    /// the journal can keep appending while they are read. Empty when the folder does not exist. I/O errors propagate.
    /// </summary>
    public IEnumerable<string> ReadLines(CancellationToken ct = default)
    {
        if (!Directory.Exists(folder)) yield break;
        var files = Directory.GetFiles(folder, "decodes-*.txt").Order(StringComparer.Ordinal).ToList();
        foreach (var path in files)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                ct.ThrowIfCancellationRequested();
                yield return line;
            }
        }
    }
}
