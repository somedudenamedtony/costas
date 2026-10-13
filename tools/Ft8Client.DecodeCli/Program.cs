// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Decoding;

namespace Ft8Client.DecodeCli;

/// <summary>Decodes WAV files through jt9 and prints one line per decode.</summary>
internal static class Program
{
    private const string Usage = """
        Usage: DecodeCli [options] <file.wav | folder> [...]
          --ft8 | --ft4         Mode (default: FT4 when the file is in an 'ft4' folder or shorter than 8 s, else FT8)
          --call CALL           My call (-c)          --grid GRID     My grid (-G)
          --dx-call CALL        DX call (-x)          --dx-grid GRID  DX grid (-g)
          --qso-progress N      QSO progress (-Q)     --depth N       Depth 1-3 (default 3)
          --golden              Use the decode context in each file's .golden.txt header
          --jt9 PATH            Path to jt9 (default: search)
          --timeout SECONDS     Kill jt9 after this long (default: one slot)
          --time                Print the decode time per file
          --keep-running        Decode every file through one jt9 that stays running (shared memory), as the app does
          --hound [--superfox]  Hound mode (and a SuperFox Fox; needs --keep-running)
          --live                With --keep-running: decode each FT8 file as if received now, waiting for the real
                                slot clock: the early pass at 11.85 s, the rest at the 13.6 s cut-off. Prints when
                                each part finished. Measures what the app does on this PC.
        """;

    private static async Task<int> Main(string[] args)
    {
        Mode? mode = null;
        string myCall = "", myGrid = "";
        string? dxCall = null, dxGrid = null, jt9 = null;
        int qso = 0, depth = 3;
        double? timeout = null;
        bool useGolden = false, showTime = false, keepRunning = false, live = false, hound = false, superFox = false;
        var inputs = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--ft8": mode = Mode.Ft8; break;
                case "--ft4": mode = Mode.Ft4; break;
                case "--call": myCall = Next(); break;
                case "--grid": myGrid = Next(); break;
                case "--dx-call": dxCall = Next(); break;
                case "--dx-grid": dxGrid = Next(); break;
                case "--qso-progress": qso = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--depth": depth = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--jt9": jt9 = Next(); break;
                case "--timeout": timeout = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--golden": useGolden = true; break;
                case "--time": showTime = true; break;
                case "--keep-running": keepRunning = true; break;
                case "--live": keepRunning = live = true; break;
                case "--hound": hound = true; break;
                case "--superfox": hound = superFox = true; break;
                case "-h" or "--help": Console.WriteLine(Usage); return 0;
                default: inputs.Add(args[i]); break;
            }
        }

        if (inputs.Count == 0) { Console.Error.WriteLine(Usage); return 2; }

        var exe = Jt9Locator.Find(jt9);
        if (exe is null) { Console.Error.WriteLine("jt9 not found. Install WSJT-X, run third_party/fetch, or pass --jt9."); return 3; }

        var files = inputs.SelectMany<string, string>(p => Directory.Exists(p)
            ? Directory.EnumerateFiles(p, "*.wav", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            : [p]).ToList();

        var temp = Path.Combine(Path.GetTempPath(), "Ft8Client.DecodeCli");
        var options = new Jt9Options(exe, temp, timeout is null ? null : TimeSpan.FromSeconds(timeout.Value));
        using var persistent = keepRunning ? new Jt9PersistentDecoder(options, Jt9Locator.Version(exe)) : null;
        IDecoder decoder = persistent ?? (IDecoder)new Jt9Decoder(options);
        if (persistent is not null) Console.WriteLine($"# {Jt9Locator.Version(exe) ?? "jt9 version unknown"}: {persistent.Description}");
        var exit = 0;
        foreach (var file in files)
        {
            var sample = SampleFile.Load(file, mode);
            var ctx = useGolden && sample.GoldenContext is not null
                ? sample.GoldenContext with { Depth = depth }
                : new DecodeContext(myCall, myGrid, dxCall, dxGrid, Depth: depth, QsoProgress: qso);
            ctx = ctx with { Hound = hound, SuperFox = superFox };
            if (files.Count > 1) Console.WriteLine($"# {file}");
            var result = live && persistent is not null && persistent.HasEarlyPass(sample.Audio.Mode)
                ? await LiveAsync(persistent, sample.Audio, ctx)
                : await decoder.DecodeAsync(sample.Audio, ctx, CancellationToken.None);
            foreach (var d in result.Decodes) Console.WriteLine(Jt9OutputParser.Format(d, ModeInfo.Separator(sample.Audio.Mode)));
            if (showTime) Console.WriteLine($"# {result.Elapsed.TotalSeconds:0.00} s");
            if (!result.Succeeded)
            {
                Console.Error.WriteLine($"{file}: {result.Status}: {result.Message}");
                exit = 1;
            }
        }
        return exit;
    }

    // Replays one slot against the real clock: the first 11.8 s at 11.85 s into a slot, the whole slot at the cut-off.
    private static async Task<DecodeResult> LiveAsync(Jt9PersistentDecoder decoder, SlotAudio audio, DecodeContext ctx)
    {
        var slot = await WaitForAsync(TimeSpan.FromSeconds(11.85));
        var partial = new short[audio.Samples12k.Length];
        Array.Copy(audio.Samples12k, partial, (int)(11.85 * ModeInfo.DecoderSampleRate));
        var early = await decoder.DecodeEarlyAsync(audio with { Samples12k = partial }, ctx, CancellationToken.None);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"# early pass: {early.Decodes.Count} decodes, done {(DateTime.UtcNow - slot).TotalSeconds:0.00} s into the slot"));
        var cutoff = slot + TimeSpan.FromSeconds(13.6);
        if (cutoff > DateTime.UtcNow) await Task.Delay(cutoff - DateTime.UtcNow);
        var full = await decoder.DecodeAsync(audio, ctx, CancellationToken.None);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"# full decode: {full.Decodes.Count} decodes, done {(DateTime.UtcNow - cutoff).TotalSeconds:0.00} s after the cut-off"));
        return full;
    }

    private static async Task<DateTime> WaitForAsync(TimeSpan intoSlot)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            var slot = SlotMath.SlotStart(now, Mode.Ft8);
            var due = slot + intoSlot;
            if (now <= due)
            {
                await Task.Delay(due - now);
                return slot;
            }
            await Task.Delay(slot + ModeInfo.SlotLength(Mode.Ft8) - now);
        }
    }
}
