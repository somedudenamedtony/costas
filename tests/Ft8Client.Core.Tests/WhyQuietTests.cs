// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Services;

namespace Ft8Client.Core.Tests;

public class WhyQuietTests
{
    private static readonly DateTime T0 = new(2026, 11, 18, 19, 0, 0, DateTimeKind.Utc);

    // Two empty slots on 20 m FT8 with a radio that answers, on the usual dial in USB data, and nothing wrong.
    private static QuietFacts Quiet() => new()
    {
        Mode = Mode.Ft8,
        Band = "20m",
        RadioSetUp = true,
        EmptySlots = 2,
        Audio = InputLevelState.Good,
        AudioDbfs = -25,
        Radio = ServiceHealth.Ok,
        DialHz = 14_074_000,
        UsualDialHz = 14_074_000,
        RigMode = "PKTUSB",
        Decoder = new ServiceStatus(ServiceHealth.Ok, "0 decodes in 0.4 s", T0),
        ClockOffsetSeconds = 0.1,
        ClockLimitSeconds = 2,
    };

    // Every check fails at once: the order decides which one is shown.
    private static QuietFacts AllWrong() => Quiet() with
    {
        Audio = InputLevelState.TooQuiet,
        DialHz = 14_090_000,
        RigMode = "LSB",
        PassbandHz = 500,
        Decoder = new ServiceStatus(ServiceHealth.Down, "jt9 not found", T0),
        ClockOffsetSeconds = 3.1,
    };

    [Fact]
    public void Explain_OneEmptySlot_Nothing() =>
        WhyQuiet.Explain(Quiet() with { EmptySlots = 1, Audio = InputLevelState.Silence }).Should().BeNull();

    [Fact]
    public void Explain_NoRadioSetUp_Nothing() =>
        WhyQuiet.Explain(AllWrong() with { RadioSetUp = false }).Should().BeNull();

    [Theory]
    [InlineData(Mode.Psk31)]
    [InlineData(Mode.Rtty)]
    public void Explain_KeyboardMode_Nothing(Mode mode) =>
        WhyQuiet.Explain(AllWrong() with { Mode = mode }).Should().BeNull();

    [Theory]
    [InlineData(InputLevelState.NoAudio, QuietCause.NoAudio, "No decodes: no audio is arriving from the radio's sound device.")]
    [InlineData(InputLevelState.Silence, QuietCause.Silence, "No decodes: the radio's sound device sends only silence.")]
    [InlineData(InputLevelState.TooQuiet, QuietCause.TooQuiet, "No decodes: the receive audio is too quiet (-52 dBFS).")]
    [InlineData(InputLevelState.Clipping, QuietCause.Clipping, "No decodes: the receive audio is clipping.")]
    public void Explain_BadAudio_SaysWhatAndHowToFix(InputLevelState audio, QuietCause cause, string starts)
    {
        var r = WhyQuiet.Explain(Quiet() with { Audio = audio, AudioDbfs = -52 });
        r!.Cause.Should().Be(cause);
        r.Text.Should().StartWith(starts);
        r.GoToDialHz.Should().BeNull();
    }

    [Theory]
    [InlineData(InputLevelState.Good)]
    [InlineData(InputLevelState.TooLoud)]
    [InlineData(InputLevelState.Waiting)]
    public void Explain_AudioNotAFault_NotTheCause(InputLevelState audio) =>
        WhyQuiet.Explain(Quiet() with { Audio = audio })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_DialOff_OffersGoToTheUsualFrequency()
    {
        var r = WhyQuiet.Explain(Quiet() with { DialHz = 14_090_000 })!;
        r.Cause.Should().Be(QuietCause.DialOff);
        r.Text.Should().Be("No decodes: the dial is on 14.090, not the usual FT8 frequency for 20 m.");
        r.GoToDialHz.Should().Be(14_074_000);
        r.GoToText.Should().Be("Go to 14.074");
    }

    [Theory]
    [InlineData(14_075_000)]
    [InlineData(14_073_000)]
    public void Explain_DialWithinOneKilohertz_NotOff(long dial) =>
        WhyQuiet.Explain(Quiet() with { DialHz = dial })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_DialNotReported_NotOff() =>
        WhyQuiet.Explain(Quiet() with { DialHz = null, RigMode = null })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_BandWithoutUsualFrequency_DialNotChecked() =>
        WhyQuiet.Explain(Quiet() with { DialHz = 14_090_000, UsualDialHz = null })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_Ft4_UsesFt4Wording()
    {
        var r = WhyQuiet.Explain(Quiet() with { Mode = Mode.Ft4, DialHz = 14_074_000, UsualDialHz = 14_080_000 })!;
        r.Text.Should().Be("No decodes: the dial is on 14.074, not the usual FT4 frequency for 20 m.");
        r.GoToText.Should().Be("Go to 14.080");
    }

    [Theory]
    [InlineData("LSB", "LSB")]
    [InlineData("PKTLSB", "LSB data")]
    [InlineData("CW", "CW")]
    [InlineData("FM", "FM")]
    public void Explain_WrongMode_SaysTheModeAndOffersGoTo(string rigMode, string shown)
    {
        var r = WhyQuiet.Explain(Quiet() with { RigMode = rigMode })!;
        r.Cause.Should().Be(QuietCause.WrongMode);
        r.Text.Should().Be($"No decodes: the radio is in {shown}, and FT8 needs USB data. Change it on the radio, or Go to 14.074 sets it.");
        r.GoToDialHz.Should().Be(14_074_000);
    }

    [Theory]
    [InlineData("USB")]
    [InlineData("PKTUSB")]
    [InlineData("pktusb")]
    public void Explain_UsbOrUsbData_ModeIsFine(string rigMode) =>
        WhyQuiet.Explain(Quiet() with { RigMode = rigMode })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_DecoderDown_SaysNotRunning()
    {
        var r = WhyQuiet.Explain(Quiet() with { Decoder = new ServiceStatus(ServiceHealth.Down, "jt9 not found", T0) })!;
        r.Cause.Should().Be(QuietCause.Decoder);
        r.Text.Should().Be("No decodes: the decoder (jt9) is not running: jt9 not found. Diagnostics has the details.");
        r.GoToDialHz.Should().BeNull();
    }

    [Fact]
    public void Explain_DecoderDegraded_SaysNotWorking()
    {
        var r = WhyQuiet.Explain(Quiet() with { Decoder = new ServiceStatus(ServiceHealth.Degraded, "jt9 exited with code 2.", T0) })!;
        r.Text.Should().Be("No decodes: the decoder (jt9) is not working: jt9 exited with code 2. Diagnostics has the details.");
    }

    [Theory]
    [InlineData(3.1, "3.1")]
    [InlineData(-2.4, "2.4")]
    public void Explain_ClockOffPastTheLimit_SaysByHowMuch(double offset, string shown)
    {
        var r = WhyQuiet.Explain(Quiet() with { ClockOffsetSeconds = offset })!;
        r.Cause.Should().Be(QuietCause.Clock);
        r.Text.Should().Be($"No decodes: your clock is {shown} s off, and decodes need it within 2 s. Sync the computer's clock, or turn on Clock correction in Settings › Transmitting.");
    }

    [Fact]
    public void Explain_ClockWithinTheLimit_NotTheCause() =>
        WhyQuiet.Explain(Quiet() with { ClockOffsetSeconds = 1.9 })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_NothingWrong_SaysTheBandMayBeQuiet()
    {
        var r = WhyQuiet.Explain(Quiet() with { EmptySlots = 4 })!;
        r.Cause.Should().Be(QuietCause.NothingFound);
        r.Text.Should().Be("No decodes in the last 4 slots. Audio, dial, radio mode, filter, decoder and clock look right, so 20 m may just be quiet.");
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1800)]
    public void Explain_NarrowFilter_SaysTheWidthAndHasNoGoTo(int width)
    {
        var r = WhyQuiet.Explain(Quiet() with { PassbandHz = width })!;
        r.Cause.Should().Be(QuietCause.NarrowFilter);
        r.Text.Should().Be($"No decodes: the radio's receive filter is {width} Hz wide, and FT8 signals spread over about 3000 Hz. Widen it on the radio to about 3000 Hz.");
        r.GoToDialHz.Should().BeNull("Costas does not change the radio's filter");
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2400)]
    [InlineData(3000)]
    [InlineData(0)]
    public void Explain_WideOrUnknownFilter_IsFine(int width) =>
        WhyQuiet.Explain(Quiet() with { PassbandHz = width })!.Cause.Should().Be(QuietCause.NothingFound);

    [Fact]
    public void Explain_NarrowFilterRadioNotConnected_DefersToTheNoRadioBanner() =>
        WhyQuiet.Explain(Quiet() with { Radio = ServiceHealth.Down, PassbandHz = 500 }).Should().BeNull();

    [Theory]
    [InlineData(ServiceHealth.Down)]
    [InlineData(ServiceHealth.Degraded)]
    public void Explain_RadioNotConnected_DefersToTheNoRadioBanner(ServiceHealth radio) =>
        WhyQuiet.Explain(Quiet() with { Radio = radio }).Should().BeNull();

    [Fact]
    public void Explain_RadioNotConnected_IgnoresItsStaleDialAndMode() =>
        WhyQuiet.Explain(Quiet() with { Radio = ServiceHealth.Down, DialHz = 14_090_000, RigMode = "LSB" }).Should().BeNull();

    [Fact]
    public void Explain_RadioNotConnectedButAudioSilent_StillSaysAudio() =>
        WhyQuiet.Explain(Quiet() with { Radio = ServiceHealth.Down, Audio = InputLevelState.Silence })!.Cause.Should().Be(QuietCause.Silence);

    // The order of confidence: audio, dial, mode, filter, decoder, clock.
    [Fact]
    public void Explain_EverythingWrong_AudioFirst() => WhyQuiet.Explain(AllWrong())!.Cause.Should().Be(QuietCause.TooQuiet);

    [Fact]
    public void Explain_AudioFine_DialBeforeMode() =>
        WhyQuiet.Explain(AllWrong() with { Audio = InputLevelState.Good })!.Cause.Should().Be(QuietCause.DialOff);

    [Fact]
    public void Explain_DialFine_ModeBeforeDecoder() =>
        WhyQuiet.Explain(AllWrong() with { Audio = InputLevelState.Good, DialHz = 14_074_000 })!.Cause.Should().Be(QuietCause.WrongMode);

    [Fact]
    public void Explain_ModeFine_FilterBeforeDecoder() =>
        WhyQuiet.Explain(AllWrong() with { Audio = InputLevelState.Good, DialHz = 14_074_000, RigMode = "PKTUSB" })!.Cause.Should().Be(QuietCause.NarrowFilter);

    [Fact]
    public void Explain_FilterFine_DecoderBeforeClock() =>
        WhyQuiet.Explain(AllWrong() with { Audio = InputLevelState.Good, DialHz = 14_074_000, RigMode = "PKTUSB", PassbandHz = 3000 })!.Cause.Should().Be(QuietCause.Decoder);

    [Fact]
    public void Explain_DecoderFine_Clock() =>
        WhyQuiet.Explain(AllWrong() with { Audio = InputLevelState.Good, DialHz = 14_074_000, RigMode = "PKTUSB", PassbandHz = 3000, Decoder = Quiet().Decoder })!
            .Cause.Should().Be(QuietCause.Clock);

    [Fact]
    public void Explain_ClockAndRadioDown_ClockBeforeDeferring() =>
        WhyQuiet.Explain(Quiet() with { Radio = ServiceHealth.Down, ClockOffsetSeconds = 3 })!.Cause.Should().Be(QuietCause.Clock);

    [Theory]
    [InlineData("PKTUSB", "USB data")]
    [InlineData("PKTFM", "FM data")]
    [InlineData("usb", "USB")]
    public void RigModeName_HamlibNames_ReadAsOperatorsKnowThem(string hamlib, string shown) =>
        WhyQuiet.RigModeName(hamlib).Should().Be(shown);
}
