// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Decoding;

namespace Ft8Client.Decoding.Tests;

public class Jt9DecoderTests
{
    private static readonly DateTime Slot = new(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc);

    [Fact]
    public async Task DecodeAsync_ProcessExceedsTimeout_KillsAndReportsTimedOut()
    {
        var dir = Directory.CreateTempSubdirectory("ft8client-hang").FullName;
        string exe;
        if (OperatingSystem.IsWindows())
        {
            exe = Path.Combine(dir, "hang.cmd");
            File.WriteAllText(exe, "@ping -n 60 127.0.0.1 > nul\r\n");
        }
        else
        {
            exe = Path.Combine(dir, "hang.sh");
            File.WriteAllText(exe, "#!/bin/sh\nsleep 60\n");
            File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var decoder = new Jt9Decoder(new Jt9Options(exe, Path.Combine(dir, "tmp"), TimeSpan.FromMilliseconds(500)));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await decoder.DecodeAsync(new SlotAudio(Slot, Mode.Ft8, new short[100]), DecodeContext.Anonymous, TestContext.Current.CancellationToken);
        sw.Stop();

        result.Status.Should().Be(DecodeStatus.TimedOut);
        result.Message.Should().Contain("exceeded");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        Directory.EnumerateDirectories(Path.Combine(dir, "tmp")).Should().BeEmpty("the scratch folder is removed after a run");
    }

    [Fact]
    public async Task DecodeAsync_MissingExecutable_ReportsFailed()
    {
        var dir = Directory.CreateTempSubdirectory("ft8client-missing").FullName;
        var decoder = new Jt9Decoder(new Jt9Options(Path.Combine(dir, "nope"), dir));
        var result = await decoder.DecodeAsync(new SlotAudio(Slot, Mode.Ft8, new short[100]), DecodeContext.Anonymous, TestContext.Current.CancellationToken);
        result.Status.Should().Be(DecodeStatus.Failed);
    }

    [Fact]
    public void BuildArguments_InContact_IncludesDxAndProgress()
    {
        var ctx = new DecodeContext("W7LIT", "DN40", "ZL2RPA", "RF70", QsoProgress: 3, RxOffsetHz: 1234);
        var args = Jt9Decoder.BuildArguments(Mode.Ft8, ctx, "/bin", "/w", "260104_025345.wav");
        string.Join(' ', args).Should().Be("-8 -d 3 -L 200 -H 3000 -f 1234 -c W7LIT -G DN40 -x ZL2RPA -g RF70 -Q 3 -e /bin -a /w -t /w 260104_025345.wav");
    }

    [Fact]
    public void BuildArguments_Ft4_UsesFt4ModeAndPeriod()
    {
        var args = Jt9Decoder.BuildArguments(Mode.Ft4, DecodeContext.Anonymous, "/bin", "/w", "x.wav");
        args.Take(3).Should().Equal("-5", "-p", "7");
    }

    [Fact]
    public void WavName_SlotStart_UsesYyMmDdHhMmSs()
    {
        Jt9Decoder.WavName(Slot).Should().Be("260104_025345.wav");
    }
}
