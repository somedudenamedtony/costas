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
using Ft8Client.Decoding.Shared;

namespace Ft8Client.Decoding.Tests;

/// <summary>
/// The decoder that keeps jt9 running. Most tests need an installed jt9 of a known version (WSJT-X 2.7 or 3.0) and
/// are skipped without one.
/// </summary>
public class Jt9PersistentDecoderTests
{
    private static readonly DateTime Slot = new(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc);

    [Theory]
    [InlineData("WSJT-X 2.7.0", true)]
    [InlineData("WSJT-X 2.7.0-rc3", true)]
    [InlineData("WSJT-X 3.0.1", true)]
    [InlineData("WSJT-X 3.0.2-devel", true)]
    [InlineData("WSJT-X 2.6.1", false)]
    [InlineData("WSJT-X 3.2.0-rc1", false)]
    [InlineData("WSJT-X 3.3.0-beta1", false)]
    [InlineData("nonsense", false)]
    [InlineData(null, false)]
    public void LayoutKnown_Version_TrueOnlyForKnownLayouts(string? version, bool known) =>
        Jt9PersistentDecoder.LayoutKnown(version).Should().Be(known);

    [Fact]
    public void SharedMemoryName_Key_MatchesQtLegacyKey()
    {
        // Qt keeps the key's letters only, then appends the SHA-1 of the whole key in lower-case hex.
        QtIpcKey.SharedMemoryName("wsjtx").Should().Be("qipc_sharedmemory_wsjtx81328f92370eaceb6d5eb29f51da099855fe66eb");
        QtIpcKey.SharedMemoryName("costas-main-1234-1").Should().Be("qipc_sharedmemory_costasmain4632b98cf5e40f65d79f42a84dd559b3a2309961");
    }

    [Fact]
    public async Task DecodeAsync_UnknownVersion_DecodesInFileMode()
    {
        var jt9 = Jt9OrSkip(out _);
        using var decoder = new Jt9PersistentDecoder(Options(jt9), "WSJT-X 9.9.0");

        var result = await decoder.DecodeAsync(Load("ft8/260101_000000.wav"), DecodeContext.Anonymous, TestContext.Current.CancellationToken);

        decoder.KeepsRunning.Should().BeFalse();
        decoder.Description.Should().Contain("9.9.0");
        decoder.ProcessStarts.Should().Be(0);
        result.Decodes.Select(d => d.Text).Should().Equal("CQ PJ4/K1ABC");
    }

    [Fact]
    public async Task DecodeAsync_NonstandardCallHeardEarlier_ResolvesHashedCall()
    {
        var jt9 = Jt9OrSkip(out var version);
        using var decoder = new Jt9PersistentDecoder(Options(jt9), version);
        var ctx = new DecodeContext("W7LIT", "DN40", null, null);

        var cq = await decoder.DecodeAsync(Load("ft8/260101_000000.wav"), ctx, TestContext.Current.CancellationToken);
        var call = await decoder.DecodeAsync(Load("ft8/260101_000015.wav"), ctx, TestContext.Current.CancellationToken);

        cq.Decodes.Select(d => d.Text).Should().Equal("CQ PJ4/K1ABC");
        call.Status.Should().Be(DecodeStatus.Ok, call.Message);
        call.Decodes.Select(d => d.Text).Should().Equal(["<PJ4/K1ABC> W7LIT DN40"], "file mode shows <...>; one running jt9 remembers the call");
        decoder.ProcessStarts.Should().Be(1, "both slots went to the same jt9");
    }

    [Fact]
    public async Task DecodeAsync_QuickDecodeWithDxCall_ResolvesHashedCallInItsOwnProcess()
    {
        var jt9 = Jt9OrSkip(out var version);
        using var decoder = new Jt9PersistentDecoder(Options(jt9), version);
        var ctx = new DecodeContext("W7LIT", "DN40", "PJ4/K1ABC", null, LowHz: 1400, HighHz: 1600, RxOffsetHz: 1500) { Quick = true };

        var result = await decoder.DecodeAsync(Load("ft8/260101_000015.wav"), ctx, TestContext.Current.CancellationToken);

        result.Decodes.Select(d => d.Text).Should().Equal("<PJ4/K1ABC> W7LIT DN40");
    }

    [Fact]
    public async Task DecodeAsync_BusySlot_FindsWhatFileModeFinds()
    {
        var jt9 = Jt9OrSkip(out var version);
        var audio = Load("ft8/210703_133430.wav");
        using var decoder = new Jt9PersistentDecoder(Options(jt9), version);
        var fileMode = new Jt9Decoder(Options(jt9));

        var kept = await decoder.DecodeAsync(audio, DecodeContext.Anonymous, TestContext.Current.CancellationToken);
        var once = await fileMode.DecodeAsync(audio, DecodeContext.Anonymous, TestContext.Current.CancellationToken);

        kept.Status.Should().Be(DecodeStatus.Ok, kept.Message);
        once.Decodes.Should().HaveCountGreaterThan(15);
        kept.Decodes.Select(d => d.Text).Should().BeEquivalentTo(once.Decodes.Select(d => d.Text));
        kept.Decodes.Should().OnlyContain(d => d.SlotStartUtc == audio.SlotStartUtc);
    }

    [Fact]
    public async Task DecodeAsync_AfterEarlyPass_ReturnsEveryDecodeOfTheSlotOnce()
    {
        var jt9 = Jt9OrSkip(out var version);
        var audio = Load("ft8/210703_133430.wav");
        using var decoder = new Jt9PersistentDecoder(Options(jt9), version);
        var partial = new short[audio.Samples12k.Length];
        Array.Copy(audio.Samples12k, partial, 141_696);

        decoder.HasEarlyPass(Mode.Ft8).Should().BeTrue();
        decoder.HasEarlyPass(Mode.Ft4).Should().BeFalse();
        // jt9 cuts the early pass short when the PC clock says the slot is nearly over, so its count depends on when the
        // test runs; the full decode finds the rest either way.
        var early = await decoder.DecodeEarlyAsync(audio with { Samples12k = partial }, DecodeContext.Anonymous, TestContext.Current.CancellationToken);
        var full = await decoder.DecodeAsync(audio, DecodeContext.Anonymous, TestContext.Current.CancellationToken);

        early.Status.Should().Be(DecodeStatus.Ok, early.Message);
        full.Status.Should().Be(DecodeStatus.Ok, full.Message);
        // The early list may be empty (FluentAssertions refuses Contain with an empty expectation), so check it is a subset.
        early.Decodes.Select(d => d.Text).Should().BeSubsetOf(full.Decodes.Select(d => d.Text));
        full.Decodes.Select(d => d.Text).Should().OnlyHaveUniqueItems();
        full.Decodes.Select(d => d.Text).Should().Contain("CQ F5RXL IN94");
        full.Decodes.Should().HaveCountGreaterThan(15);
    }

    [Fact]
    public async Task DecodeAsync_Ft4Slot_Decodes()
    {
        var jt9 = Jt9OrSkip(out var version);
        var audio = Load("ft4/000000_000002.wav");
        using var decoder = new Jt9PersistentDecoder(Options(jt9), version);

        var result = await decoder.DecodeAsync(audio, DecodeContext.Anonymous, TestContext.Current.CancellationToken);
        var once = await new Jt9Decoder(Options(jt9)).DecodeAsync(audio, DecodeContext.Anonymous, TestContext.Current.CancellationToken);

        result.Status.Should().Be(DecodeStatus.Ok, result.Message);
        once.Decodes.Should().NotBeEmpty();
        result.Decodes.Select(d => d.Text).Should().BeEquivalentTo(once.Decodes.Select(d => d.Text));
    }

    [Fact]
    public async Task DecodeAsync_Jt9NeverAnswers_FallsBackToFileModeAfterThreeTries()
    {
        Assert.SkipUnless(SharedMemorySegment.Supported, "Shared memory with jt9 needs Windows or Linux.");
        var dir = Directory.CreateTempSubdirectory("ft8client-noshm").FullName;
        string exe;
        if (OperatingSystem.IsWindows())
        {
            exe = Path.Combine(dir, "quit.cmd");
            File.WriteAllText(exe, "@exit /b 3\r\n");
        }
        else
        {
            exe = Path.Combine(dir, "quit.sh");
            File.WriteAllText(exe, "#!/bin/sh\nexit 3\n");
            File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        using var decoder = new Jt9PersistentDecoder(new Jt9Options(exe, Path.Combine(dir, "tmp"), TimeSpan.FromSeconds(10)), "WSJT-X 3.0.1");
        var audio = new SlotAudio(Slot, Mode.Ft8, new short[ModeInfo.DecoderSamples(Mode.Ft8)]);

        for (var i = 0; i < 3; i++) await decoder.DecodeAsync(audio, DecodeContext.Anonymous, TestContext.Current.CancellationToken);

        decoder.KeepsRunning.Should().BeFalse();
        decoder.Description.Should().Contain("did not answer through shared memory");
        decoder.HasEarlyPass(Mode.Ft8).Should().BeFalse();
        Directory.EnumerateDirectories(Path.Combine(dir, "tmp"), "jt9-shm-*").Should().BeEmpty("a failed process's folder is removed");
    }

    private static string Jt9OrSkip(out string? version)
    {
        var jt9 = Jt9Locator.Find();
        Assert.SkipWhen(jt9 is null, "jt9 is not installed; run third_party/fetch or install WSJT-X.");
        version = Jt9Locator.Version(jt9!);
        Assert.SkipUnless(SharedMemorySegment.Supported && Jt9PersistentDecoder.LayoutKnown(version),
            $"jt9 {version ?? "of unknown version"} cannot be kept running here.");
        return jt9!;
    }

    private static Jt9Options Options(string jt9) =>
        new(jt9, Path.Combine(Path.GetTempPath(), "ft8client-tests", Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(60));

    private static SlotAudio Load(string relative) => SampleFile.Load(Path.Combine(TestPaths.Samples, relative)).Audio;
}
