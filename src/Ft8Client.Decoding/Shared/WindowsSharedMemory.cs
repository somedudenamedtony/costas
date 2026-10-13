// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace Ft8Client.Decoding.Shared;

/// <summary>
/// A named file mapping backed by the page file, as <c>QSharedMemory::create</c> makes on Windows; <c>jt9</c> opens it
/// with <c>OpenFileMapping</c> by the same name. Windows frees it when the last handle closes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSharedMemory : SharedMemorySegment
{
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _view;
    private bool _addedRef;

    public WindowsSharedMemory(string name, int size)
    {
        _file = MemoryMappedFile.CreateNew(name, size, MemoryMappedFileAccess.ReadWrite);
        try
        {
            _view = _file.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);
            _view.SafeMemoryMappedViewHandle.DangerousAddRef(ref _addedRef);
            Base = _view.SafeMemoryMappedViewHandle.DangerousGetHandle() + (nint)_view.PointerOffset;
            Size = size;
        }
        catch
        {
            _file.Dispose();
            throw;
        }
        Clear(0, size);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (_addedRef) _view.SafeMemoryMappedViewHandle.DangerousRelease();
        _addedRef = false;
        Base = IntPtr.Zero;
        _view.Dispose();
        _file.Dispose();
    }
}
