// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.InteropServices;

namespace Ft8Client.Decoding.Shared;

/// <summary>
/// A shared memory segment that a <c>jt9 -s KEY</c> process can attach to with <c>QSharedMemory</c>. Costas creates
/// it, <c>jt9</c> attaches. Windows uses a named file mapping, Linux a System V segment keyed by <c>ftok</c> on a file
/// in the temp folder; other systems are not supported and use file mode.
/// </summary>
internal abstract class SharedMemorySegment : IDisposable
{
    /// <summary>Start of the mapped memory.</summary>
    public IntPtr Base { get; protected set; }

    /// <summary>Size in bytes.</summary>
    public int Size { get; protected set; }

    /// <summary>True on systems where Costas can create a segment <c>jt9</c> attaches to.</summary>
    public static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    /// <summary>
    /// Creates a zeroed segment for the key. <paramref name="tempDir"/> is the temp folder <c>jt9</c> is given (its
    /// <c>TMPDIR</c>), which Qt uses for the key file on Unix. Throws <see cref="IOException"/> when it cannot.
    /// </summary>
    public static SharedMemorySegment Create(string key, int size, string tempDir)
    {
        if (OperatingSystem.IsWindows()) return new WindowsSharedMemory(QtIpcKey.SharedMemoryName(key), size);
        if (OperatingSystem.IsLinux()) return new SysVSharedMemory(Path.Combine(tempDir, QtIpcKey.SharedMemoryName(key)), size);
        throw new PlatformNotSupportedException("Shared memory with jt9 needs Windows or Linux.");
    }

    /// <summary>Called once <c>jt9</c> has attached; a Unix segment is then marked for removal when both sides let go.</summary>
    public virtual void Attached()
    {
    }

    /// <summary>Reads a 32-bit integer.</summary>
    public int ReadInt32(int offset) => Marshal.ReadInt32(Base, offset);

    /// <summary>Writes a 32-bit integer.</summary>
    public void WriteInt32(int offset, int value) => Marshal.WriteInt32(Base, offset, value);

    /// <summary>Writes a C <c>bool</c>.</summary>
    public void WriteBool(int offset, bool value) => Marshal.WriteByte(Base, offset, value ? (byte)1 : (byte)0);

    /// <summary>Writes a 32-bit float.</summary>
    public void WriteSingle(int offset, float value) => Marshal.WriteInt32(Base, offset, BitConverter.SingleToInt32Bits(value));

    /// <summary>Writes a fixed-width, space-padded Latin-1 field (a Fortran character array).</summary>
    public void WriteText(int offset, string text, int width)
    {
        var bytes = new byte[width];
        Array.Fill(bytes, (byte)' ');
        for (var i = 0; i < Math.Min(width, text.Length); i++) bytes[i] = text[i] < 256 ? (byte)text[i] : (byte)'?';
        Marshal.Copy(bytes, 0, Base + offset, width);
    }

    /// <summary>Writes 16-bit samples and zeroes the rest of <paramref name="count"/>.</summary>
    public void WriteSamples(int offset, short[] samples, int length, int count)
    {
        var n = Math.Clamp(length, 0, Math.Min(samples.Length, count));
        Marshal.Copy(samples, 0, Base + offset, n);
        Clear(offset + 2 * n, 2 * (count - n));
    }

    /// <summary>Zeroes a range.</summary>
    public unsafe void Clear(int offset, int length)
    {
        if (length > 0) new Span<byte>((byte*)Base + offset, length).Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the mapping.</summary>
    protected abstract void Dispose(bool disposing);
}
