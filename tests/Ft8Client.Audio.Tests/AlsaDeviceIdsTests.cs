// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;

namespace Ft8Client.Audio.Tests;

public class AlsaDeviceIdsTests
{
    private static readonly string[] Ids =
    [
        "ALSA:HDA Intel PCH: ALC892 Analog (hw:0,0)",
        "ALSA:USB Audio CODEC: - (hw:2,0)",
        "ALSA:pulse",
        "ALSA:default",
    ];

    [Fact]
    public void Find_ExactId_ReturnsIt() => AlsaDeviceIds.Find(Ids, "ALSA:pulse").Should().Be(2);

    [Fact]
    public void Find_UsbCodecOnAnotherCardNumber_ReturnsIt() =>
        AlsaDeviceIds.Find(Ids, "ALSA:USB Audio CODEC: - (hw:1,0)").Should().Be(1);

    [Fact]
    public void Find_UnknownDevice_ReturnsMinusOne() => AlsaDeviceIds.Find(Ids, "ALSA:Something else (hw:1,0)").Should().Be(-1);

    [Fact]
    public void WithoutCardNumber_HwSuffix_IsRemoved() =>
        AlsaDeviceIds.WithoutCardNumber("ALSA:USB Audio CODEC: - (hw:2,0)").Should().Be("ALSA:USB Audio CODEC: -");
}
