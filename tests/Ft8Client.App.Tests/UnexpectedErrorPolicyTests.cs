// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;

namespace Ft8Client.App.Tests;

public class UnexpectedErrorPolicyTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Survive_OccasionalErrors_CarriesOn()
    {
        var p = new UnexpectedErrorPolicy(limit: 3, window: TimeSpan.FromSeconds(30));
        p.Survive(T0).Should().BeTrue();
        p.Survive(T0.AddMinutes(1)).Should().BeTrue();
        p.Survive(T0.AddMinutes(2)).Should().BeTrue();
        p.Survive(T0.AddMinutes(3)).Should().BeTrue();
    }

    [Fact]
    public void Survive_ErrorsRepeatingQuickly_EndsTheApp()
    {
        var p = new UnexpectedErrorPolicy(limit: 3, window: TimeSpan.FromSeconds(30));
        p.Survive(T0).Should().BeTrue();
        p.Survive(T0.AddSeconds(1)).Should().BeTrue();
        p.Survive(T0.AddSeconds(2)).Should().BeTrue();
        p.Survive(T0.AddSeconds(3)).Should().BeFalse();
    }
}
