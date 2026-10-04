// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;

namespace Ft8Client.App.Engine;

/// <summary>Slot audio from the sound card through the slot recorder.</summary>
public sealed class LiveSlotSource(SlotRecorder recorder) : ISlotSource
{
    /// <inheritdoc />
    public void BeginSlot(DateTime slotStartUtc, Mode mode) => recorder.BeginSlot(slotStartUtc, mode);

    /// <inheritdoc />
    public void CancelSlot() => recorder.Cancel();

    /// <inheritdoc />
    public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode) => recorder.Cutoff();
}
