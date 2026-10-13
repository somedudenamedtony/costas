// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ft8Client.Audio.Backends;

/// <summary>
/// The parts of PortAudio v19 (<c>portaudio.h</c>) the backend uses: blocking streams of 32-bit float samples. The
/// library is the distribution's <c>libportaudio2</c> package.
/// </summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class PortAudioNative
{
    public const string Library = "libportaudio.so.2";
    public const int NoError = 0;
    public const int FormatIsSupported = 0;
    public const int OutputUnderflowed = -9980;
    public const int InputOverflowed = -9981;
    public const ulong Float32 = 0x1;
    public const ulong ClipOff = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfo
    {
        public int StructVersion;
        public IntPtr Name;
        public int HostApi;
        public int MaxInputChannels;
        public int MaxOutputChannels;
        public double DefaultLowInputLatency;
        public double DefaultLowOutputLatency;
        public double DefaultHighInputLatency;
        public double DefaultHighOutputLatency;
        public double DefaultSampleRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HostApiInfo
    {
        public int StructVersion;
        public int Type;
        public IntPtr Name;
        public int DeviceCount;
        public int DefaultInputDevice;
        public int DefaultOutputDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StreamParameters
    {
        public int Device;
        public int ChannelCount;
        public CULong SampleFormat;
        public double SuggestedLatency;
        public IntPtr HostApiSpecificStreamInfo;
    }

    [LibraryImport(Library, EntryPoint = "Pa_Initialize")]
    public static partial int Initialize();

    [LibraryImport(Library, EntryPoint = "Pa_Terminate")]
    public static partial int Terminate();

    [LibraryImport(Library, EntryPoint = "Pa_GetErrorText")]
    public static partial IntPtr GetErrorText(int error);

    [LibraryImport(Library, EntryPoint = "Pa_GetDeviceCount")]
    public static partial int GetDeviceCount();

    [LibraryImport(Library, EntryPoint = "Pa_GetDeviceInfo")]
    public static partial DeviceInfo* GetDeviceInfo(int device);

    [LibraryImport(Library, EntryPoint = "Pa_GetHostApiInfo")]
    public static partial HostApiInfo* GetHostApiInfo(int hostApi);

    [LibraryImport(Library, EntryPoint = "Pa_GetDefaultOutputDevice")]
    public static partial int GetDefaultOutputDevice();

    [LibraryImport(Library, EntryPoint = "Pa_IsFormatSupported")]
    public static partial int IsFormatSupported(StreamParameters* input, StreamParameters* output, double sampleRate);

    [LibraryImport(Library, EntryPoint = "Pa_OpenStream")]
    public static partial int OpenStream(out IntPtr stream, StreamParameters* input, StreamParameters* output, double sampleRate,
        CULong framesPerBuffer, CULong flags, IntPtr callback, IntPtr userData);

    [LibraryImport(Library, EntryPoint = "Pa_StartStream")]
    public static partial int StartStream(IntPtr stream);

    [LibraryImport(Library, EntryPoint = "Pa_StopStream")]
    public static partial int StopStream(IntPtr stream);

    [LibraryImport(Library, EntryPoint = "Pa_CloseStream")]
    public static partial int CloseStream(IntPtr stream);

    [LibraryImport(Library, EntryPoint = "Pa_ReadStream")]
    public static partial int ReadStream(IntPtr stream, float* buffer, CULong frames);

    [LibraryImport(Library, EntryPoint = "Pa_WriteStream")]
    public static partial int WriteStream(IntPtr stream, float* buffer, CULong frames);

    /// <summary>PortAudio's text for an error code.</summary>
    public static string ErrorText(int error) => Marshal.PtrToStringUTF8(GetErrorText(error)) ?? $"PortAudio error {error}";

    /// <summary>Throws <see cref="InvalidOperationException"/> with PortAudio's text when <paramref name="error"/> is not 0.</summary>
    public static void Check(int error, string what)
    {
        if (error != NoError) throw new InvalidOperationException($"{what}: {ErrorText(error)}");
    }
}
