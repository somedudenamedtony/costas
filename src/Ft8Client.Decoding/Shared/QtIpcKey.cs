// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Security.Cryptography;
using System.Text;

namespace Ft8Client.Decoding.Shared;

/// <summary>
/// The native name Qt gives a <c>QSharedMemory</c> key (<c>QSharedMemoryPrivate::makePlatformSafeKey</c> in Qt 5, the
/// legacy key in Qt 6): the prefix, the key's ASCII letters, then the key's SHA-1 in lower-case hex. <c>jt9 -s KEY</c>
/// attaches to the segment by this name, so Costas creates it under the same name.
/// </summary>
internal static class QtIpcKey
{
    /// <summary>The name of a shared memory segment: a file mapping name on Windows, a file name in the temp folder on Unix.</summary>
    public static string SharedMemoryName(string key)
    {
        var sb = new StringBuilder("qipc_sharedmemory_");
        foreach (var ch in key)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z') sb.Append(ch);
        }
        sb.Append(Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(key))));
        return sb.ToString();
    }
}
