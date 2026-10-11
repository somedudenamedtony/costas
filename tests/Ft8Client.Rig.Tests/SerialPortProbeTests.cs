// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig.Tests;

public class SerialPortProbeTests
{
    [Fact]
    public void Classify_AccessDenied_InUseOnWindowsOnly()
    {
        // On Windows SerialPort.Open throws this when another program has the port open. On Linux it wraps every open
        // failure (captured with .NET 10: a missing /dev/ttyX gives "Access to the port is denied", inner "Unknown error 203").
        SerialPortProbe.Classify(new UnauthorizedAccessException("Access to the port 'COM4' is denied."))
            .Should().Be(OperatingSystem.IsWindows() ? PortOpenResult.InUse : PortOpenResult.Failed);
    }

    [Theory]
    [InlineData("The port 'COM9' does not exist.")]
    [InlineData("Could not find file '/dev/ttyUSB9'. No such file or directory")]
    public void Classify_PortNotThere_Missing(string message)
    {
        SerialPortProbe.Classify(new IOException(message)).Should().Be(PortOpenResult.Missing);
    }

    [Fact]
    public void Classify_FileNotFound_Missing()
    {
        SerialPortProbe.Classify(new FileNotFoundException("gone")).Should().Be(PortOpenResult.Missing);
    }

    [Fact]
    public void Classify_OtherIoError_Failed()
    {
        SerialPortProbe.Classify(new IOException("The semaphore timeout period has expired.")).Should().Be(PortOpenResult.Failed);
    }

    [Fact]
    public void TryOpen_PortThatDoesNotExist_NotOpenedWithDetail()
    {
        var name = OperatingSystem.IsWindows() ? "COM250" : "/dev/ttyCostasNoSuchPort";
        var (result, detail) = SerialPortProbe.TryOpen(name);
        result.Should().NotBe(PortOpenResult.Opened);
        detail.Should().NotBeNullOrEmpty();
    }
}
