// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;

namespace Ft8Client.Decoding;

/// <summary>
/// A recorded slot from the sample corpus: the WAV, its slot time and mode from the file name and folder,
/// and the decode context and expected output from its <c>.golden.txt</c> companion, when present.
/// </summary>
public sealed record SampleFile(string Path, SlotAudio Audio, DecodeContext? GoldenContext, IReadOnlyList<string>? GoldenLines)
{
    /// <summary>Loads a sample WAV and its golden file.</summary>
    public static SampleFile Load(string path, Mode? mode = null)
    {
        var (samples, rate) = WavFile.Read(path);
        if (rate != ModeInfo.DecoderSampleRate)
            throw new InvalidDataException($"{path}: sample rate {rate} Hz; samples must be 12000 Hz.");
        var resolved = mode ?? GuessMode(path, samples.Length);
        var slot = SlotTimeFromName(System.IO.Path.GetFileNameWithoutExtension(path));

        var goldenPath = System.IO.Path.ChangeExtension(path, ".golden.txt");
        DecodeContext? ctx = null;
        List<string>? lines = null;
        if (File.Exists(goldenPath))
        {
            lines = [];
            ctx = DecodeContext.Anonymous;
            foreach (var line in File.ReadAllLines(goldenPath))
            {
                if (line.StartsWith("# args:", StringComparison.Ordinal)) ctx = ParseArgs(line["# args:".Length..]);
                else if (!line.StartsWith('#')) lines.Add(line);
            }
        }
        return new SampleFile(path, new SlotAudio(slot, resolved, samples), ctx, lines);
    }

    /// <summary>The slot start encoded in a <c>YYMMDD_HHMMSS</c> name; 2000-01-01 when the date part is zero.</summary>
    public static DateTime SlotTimeFromName(string name)
    {
        if (name.Length >= 13 && name[6] == '_' &&
            int.TryParse(name.AsSpan(0, 6), NumberStyles.None, CultureInfo.InvariantCulture, out var date) &&
            int.TryParse(name.AsSpan(7, 6), NumberStyles.None, CultureInfo.InvariantCulture, out var time))
        {
            var y = 2000 + date / 10000;
            var mo = Math.Max(1, date / 100 % 100);
            var d = Math.Max(1, date % 100);
            try
            {
                return new DateTime(y, mo, d, time / 10000, time / 100 % 100, time % 100, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException) { }
        }
        return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static Mode GuessMode(string path, int sampleCount)
    {
        var dir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? string.Empty);
        if (string.Equals(dir, "ft4", StringComparison.OrdinalIgnoreCase)) return Mode.Ft4;
        if (string.Equals(dir, "ft8", StringComparison.OrdinalIgnoreCase)) return Mode.Ft8;
        return sampleCount < 8 * ModeInfo.DecoderSampleRate ? Mode.Ft4 : Mode.Ft8;
    }

    private static DecodeContext ParseArgs(string text)
    {
        var t = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string call = "", grid = "";
        string? dx = null, dxGrid = null;
        var qso = 0;
        for (var i = 0; i + 1 < t.Length; i++)
        {
            switch (t[i])
            {
                case "-c": call = t[++i]; break;
                case "-G": grid = t[++i]; break;
                case "-x": dx = t[++i]; break;
                case "-g": dxGrid = t[++i]; break;
                case "-Q": qso = int.Parse(t[++i], CultureInfo.InvariantCulture); break;
            }
        }
        return new DecodeContext(call, grid, dx, dxGrid, QsoProgress: qso);
    }
}
