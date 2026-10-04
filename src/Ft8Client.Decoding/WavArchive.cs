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

/// <summary>Saves slot audio as <c>YYMMDD_HHMMSS.wav</c> (12 kHz, 16-bit mono, as WSJT-X does) and deletes old files.</summary>
public sealed class WavArchive(string folder)
{
    /// <summary>Saves a slot. Returns the path.</summary>
    public string Save(SlotAudio audio)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Jt9Decoder.WavName(audio.SlotStartUtc));
        WavFile.Write(path, audio.Samples12k, ModeInfo.DecoderSampleRate);
        return path;
    }

    /// <summary>Whether a slot should be saved under a <c>none</c>/<c>decoded</c>/<c>all</c> setting.</summary>
    public static bool ShouldSave(string setting, int decodeCount) => setting switch
    {
        "all" => true,
        "decoded" => decodeCount > 0,
        _ => false,
    };

    /// <summary>Deletes saved files whose slot time (from the name) is older than <paramref name="days"/>. Returns the count.</summary>
    public int Prune(DateTime nowUtc, int days)
    {
        if (!Directory.Exists(folder) || days <= 0) return 0;
        var cut = nowUtc.AddDays(-days);
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(folder, "*.wav"))
        {
            if (!DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyMMdd_HHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) || t >= cut) continue;
            try
            {
                File.Delete(f);
                n++;
            }
            catch (IOException)
            {
            }
        }
        return n;
    }
}
