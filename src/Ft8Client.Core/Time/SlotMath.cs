// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Time;

/// <summary>Slot arithmetic on UTC times.</summary>
public static class SlotMath
{
    /// <summary>The start of the slot containing <paramref name="utc"/>.</summary>
    public static DateTime SlotStart(DateTime utc, Mode mode)
    {
        var len = ModeInfo.SlotLength(mode).Ticks;
        var day = utc.Date;
        var into = (utc - day).Ticks;
        return new DateTime(day.Ticks + into / len * len, DateTimeKind.Utc);
    }

    /// <summary>Index of the slot within the UTC day.</summary>
    public static long SlotIndex(DateTime slotStartUtc, Mode mode) =>
        (slotStartUtc - slotStartUtc.Date).Ticks / ModeInfo.SlotLength(mode).Ticks;

    /// <summary>Parity of the slot starting at <paramref name="slotStartUtc"/>.</summary>
    public static SlotParity Parity(DateTime slotStartUtc, Mode mode) =>
        SlotIndex(slotStartUtc, mode) % 2 == 0 ? SlotParity.Even : SlotParity.Odd;

    /// <summary>The opposite parity.</summary>
    public static SlotParity Opposite(SlotParity p) => p == SlotParity.Even ? SlotParity.Odd : SlotParity.Even;

    /// <summary>The first slot start strictly after <paramref name="slotStartUtc"/> with the given parity.</summary>
    public static DateTime NextSlotOfParity(DateTime slotStartUtc, SlotParity parity, Mode mode)
    {
        var next = slotStartUtc + ModeInfo.SlotLength(mode);
        return Parity(next, mode) == parity ? next : next + ModeInfo.SlotLength(mode);
    }
}
