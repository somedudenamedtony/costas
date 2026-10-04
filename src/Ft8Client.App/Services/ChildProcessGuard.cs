// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Ft8Client.Core.Processes;

namespace Ft8Client.App.Services;

/// <summary>
/// Makes child processes (jt9, rigctld) die with the app: a Windows job object with kill-on-close, and on every
/// platform a list of children killed on exit.
/// </summary>
public sealed partial class ChildProcessGuard : IProcessGuard, IDisposable
{
    private readonly List<Process> _children = [];
    private readonly IntPtr _job;

    /// <summary>Creates the guard.</summary>
    public ChildProcessGuard()
    {
        if (OperatingSystem.IsWindows()) _job = CreateKillOnCloseJob();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    /// <inheritdoc />
    public void Adopt(Process process)
    {
        if (OperatingSystem.IsWindows() && _job != IntPtr.Zero)
        {
            try { AssignProcessToJobObject(_job, process.Handle); }
            catch (InvalidOperationException) { }
        }
        lock (_children)
        {
            _children.RemoveAll(p => { try { return p.HasExited; } catch (InvalidOperationException) { return true; } });
            _children.Add(process);
        }
    }

    /// <summary>Kills every child still running.</summary>
    public void Dispose()
    {
        lock (_children)
        {
            foreach (var p in _children)
            {
                try
                {
                    if (!p.HasExited) p.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            _children.Clear();
        }
    }

    private static IntPtr CreateKillOnCloseJob()
    {
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return IntPtr.Zero;
        var info = new JobObjectExtendedLimitInformation { BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = 0x2000 } };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            SetInformationJobObject(job, 9, ptr, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return job;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
