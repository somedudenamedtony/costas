// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>Character sets used by the 77-bit message packing. From ft8_lib ft8/text.c (MIT, Kārlis Goba).</summary>
internal static class CharTables
{
    public const string Full = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ+-./?";
    public const string AlnumSpaceSlash = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ/";
    public const string AlnumSpace = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Alnum = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LettersSpace = " ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Numeric = "0123456789";

    public static int Index(char c, string table) => table.IndexOf(c, StringComparison.Ordinal);
}
