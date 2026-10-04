// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Decoding;

namespace Ft8Client.App.Engine;

/// <summary>Supplies the received audio of a slot at the capture cut-off.</summary>
public interface ISlotSource
{
    /// <summary>Called at slot start; the source begins collecting.</summary>
    void BeginSlot(DateTime slotStartUtc, Mode mode);

    /// <summary>Called when the slot will not be decoded (we transmit in it).</summary>
    void CancelSlot();

    /// <summary>The slot's audio at cut-off, or null when nothing was collected.</summary>
    SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode);
}
