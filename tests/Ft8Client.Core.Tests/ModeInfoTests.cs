// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;

namespace Ft8Client.Core.Tests;

public class ModeInfoTests
{
    [Fact]
    public void TxEnd_Ft8_IsHalfSecondPlus79SymbolsOf160Ms() => ModeInfo.TxEnd(Mode.Ft8).Should().Be(TimeSpan.FromMilliseconds(13_140));

    [Fact]
    public void TxEnd_Ft4_IsHalfSecondPlus105SymbolsOf48Ms() => ModeInfo.TxEnd(Mode.Ft4).Should().Be(TimeSpan.FromMilliseconds(5_540));
}
