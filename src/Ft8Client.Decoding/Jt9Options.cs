// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Decoding;

/// <summary>How to run <c>jt9</c>.</summary>
/// <param name="ExecutablePath">Full path of <c>jt9</c>.</param>
/// <param name="TempRoot">Folder under which a scratch folder is created per run.</param>
/// <param name="Timeout">Kill the run after this long. Defaults to one slot length.</param>
/// <param name="Threads">Decoder threads for the multithreaded FT8 decoder (<c>-M -N</c>, WSJT-X 3.x only). 0 = single-threaded.</param>
public sealed record Jt9Options(string ExecutablePath, string TempRoot, TimeSpan? Timeout = null, int Threads = 0);
