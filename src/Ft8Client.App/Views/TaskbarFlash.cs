// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Ft8Client.App.Views;

/// <summary>Flashes a window's taskbar button until it is brought forward (Windows only; elsewhere it does nothing).</summary>
internal static partial class TaskbarFlash
{
    private const uint FlashTray = 0x2;
    private const uint FlashUntilForeground = 0xC;

    /// <summary>Flashes <paramref name="window"/> unless it is already the active window.</summary>
    public static void Flash(Window window)
    {
        if (!OperatingSystem.IsWindows() || window.IsActive || window.TryGetPlatformHandle() is not { } handle) return;
        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Hwnd = handle.Handle,
            Flags = FlashTray | FlashUntilForeground,
            Count = 0,
            Timeout = 0,
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashInfo info);
}
