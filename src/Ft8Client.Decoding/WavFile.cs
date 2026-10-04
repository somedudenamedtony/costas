// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Buffers.Binary;
using System.Text;

namespace Ft8Client.Decoding;

/// <summary>Minimal reader and writer for 16-bit PCM mono WAV files, the format <c>jt9</c> reads.</summary>
public static class WavFile
{
    /// <summary>Writes 16-bit mono PCM.</summary>
    public static void Write(string path, ReadOnlySpan<short> samples, int sampleRate)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(fs, samples, sampleRate);
    }

    /// <summary>Writes 16-bit mono PCM to a stream.</summary>
    public static void Write(Stream stream, ReadOnlySpan<short> samples, int sampleRate)
    {
        var dataBytes = samples.Length * 2;
        Span<byte> header = stackalloc byte[44];
        Encoding.ASCII.GetBytes("RIFF", header[0..4]);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], 36 + dataBytes);
        Encoding.ASCII.GetBytes("WAVE", header[8..12]);
        Encoding.ASCII.GetBytes("fmt ", header[12..16]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..22], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..24], 1);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..28], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..32], sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..34], 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..36], 16);
        Encoding.ASCII.GetBytes("data", header[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..44], dataBytes);
        stream.Write(header);

        var buffer = new byte[Math.Min(dataBytes, 65536)];
        var i = 0;
        while (i < samples.Length)
        {
            var n = Math.Min(buffer.Length / 2, samples.Length - i);
            for (var k = 0; k < n; k++) BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(k * 2, 2), samples[i + k]);
            stream.Write(buffer, 0, n * 2);
            i += n;
        }
    }

    /// <summary>Reads a PCM WAV file. Multi-channel files are mixed to mono; 8, 16, 24 and 32-bit PCM and 32-bit float are accepted.</summary>
    public static (short[] Samples, int SampleRate) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Read(bytes);
    }

    /// <summary>Reads PCM WAV bytes.</summary>
    public static (short[] Samples, int SampleRate) Read(byte[] bytes)
    {
        if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
            throw new InvalidDataException("Not a RIFF/WAVE file.");

        int format = 0, channels = 0, rate = 0, bits = 0;
        var pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            var id = Encoding.ASCII.GetString(bytes, pos, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos + 4, 4));
            var body = pos + 8;
            if (size < 0 || body + size > bytes.Length) size = bytes.Length - body;
            if (id == "fmt ")
            {
                format = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body, 2));
                channels = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + 2, 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(body + 4, 4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + 14, 2));
                if (format == 0xFFFE && size >= 26) format = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + 24, 2));
            }
            else if (id == "data")
            {
                if (channels <= 0 || bits <= 0) throw new InvalidDataException("WAV data chunk before fmt chunk.");
                return (Decode(bytes.AsSpan(body, size), format, channels, bits), rate);
            }
            pos = body + size + (size & 1);
        }
        throw new InvalidDataException("WAV file has no data chunk.");
    }

    private static short[] Decode(ReadOnlySpan<byte> data, int format, int channels, int bits)
    {
        var bytesPerSample = bits / 8;
        var frames = data.Length / (bytesPerSample * channels);
        var result = new short[frames];
        for (var f = 0; f < frames; f++)
        {
            double sum = 0;
            for (var c = 0; c < channels; c++)
            {
                var s = data.Slice((f * channels + c) * bytesPerSample, bytesPerSample);
                sum += (format, bits) switch
                {
                    (1, 8) => (s[0] - 128) / 128.0,
                    (1, 16) => BinaryPrimitives.ReadInt16LittleEndian(s) / 32768.0,
                    (1, 24) => ((s[0] | (s[1] << 8) | ((sbyte)s[2] << 16))) / 8388608.0,
                    (1, 32) => BinaryPrimitives.ReadInt32LittleEndian(s) / 2147483648.0,
                    (3, 32) => BinaryPrimitives.ReadSingleLittleEndian(s),
                    _ => throw new InvalidDataException($"Unsupported WAV format {format} with {bits} bits."),
                };
            }
            var v = sum / channels * 32767.0;
            result[f] = (short)Math.Clamp(Math.Round(v), short.MinValue, short.MaxValue);
        }
        return result;
    }
}
