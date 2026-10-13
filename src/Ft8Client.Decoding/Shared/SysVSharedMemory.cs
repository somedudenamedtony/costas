// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ft8Client.Decoding.Shared;

/// <summary>
/// A System V segment keyed by <c>ftok(file, 'Q')</c>, as Qt's <c>QSharedMemory</c> makes on Linux: the key file must
/// exist while <c>jt9</c> attaches. Once it has attached, the segment is marked for removal, so the kernel frees it
/// when both processes let go, even if Costas is killed.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed partial class SysVSharedMemory : SharedMemorySegment
{
    private const int IpcCreat = 0x200;
    private const int IpcExcl = 0x400;
    private const int IpcRmid = 0;
    private readonly string _keyFile;
    private readonly int _id;
    private bool _removed;

    public SysVSharedMemory(string keyFile, int size)
    {
        _keyFile = keyFile;
        File.WriteAllBytes(keyFile, []);
        var key = Ftok(keyFile, 'Q');
        if (key == -1) throw new IOException($"ftok failed for {keyFile} (errno {Marshal.GetLastPInvokeError()}).");
        _id = ShmGet(key, (nuint)size, IpcCreat | IpcExcl | 0x180); // 0600
        if (_id == -1) throw new IOException($"shmget failed (errno {Marshal.GetLastPInvokeError()}).");
        var addr = ShmAt(_id, IntPtr.Zero, 0);
        if (addr == new IntPtr(-1))
        {
            var errno = Marshal.GetLastPInvokeError();
            _ = ShmCtl(_id, IpcRmid, IntPtr.Zero);
            throw new IOException($"shmat failed (errno {errno}).");
        }
        Base = addr;
        Size = size;
        Clear(0, size);
    }

    public override void Attached() => Remove();

    protected override void Dispose(bool disposing)
    {
        if (Base != IntPtr.Zero) _ = ShmDt(Base);
        Base = IntPtr.Zero;
        Remove();
        try { File.Delete(_keyFile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void Remove()
    {
        if (_removed) return;
        _removed = true;
        _ = ShmCtl(_id, IpcRmid, IntPtr.Zero);
    }

    [LibraryImport("libc", EntryPoint = "ftok", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Ftok(string path, int id);

    [LibraryImport("libc", EntryPoint = "shmget", SetLastError = true)]
    private static partial int ShmGet(int key, nuint size, int flags);

    [LibraryImport("libc", EntryPoint = "shmat", SetLastError = true)]
    private static partial IntPtr ShmAt(int id, IntPtr address, int flags);

    [LibraryImport("libc", EntryPoint = "shmdt", SetLastError = true)]
    private static partial int ShmDt(IntPtr address);

    [LibraryImport("libc", EntryPoint = "shmctl", SetLastError = true)]
    private static partial int ShmCtl(int id, int command, IntPtr buffer);
}
