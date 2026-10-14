// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems;

/// <summary>One signal being read.</summary>
public sealed class ReceiveChannel
{
    internal ReceiveChannel(int id, IKeyboardReceiver receiver, long createdSample)
    {
        Id = id;
        Receiver = receiver;
        LastTextSample = createdSample;
        TunedSample = createdSample;
    }

    /// <summary>The channel's number, stable while it lives.</summary>
    public int Id { get; }

    /// <summary>Its receiver. A quiet channel gets a new one when a signal turns up near it (see <see cref="ChannelBank"/>).</summary>
    public IKeyboardReceiver Receiver { get; internal set; }

    /// <summary>Kept even when quiet (the operator chose it).</summary>
    public bool Pinned { get; internal set; }

    /// <summary>The input sample count when it last produced text.</summary>
    internal long LastTextSample { get; set; }

    /// <summary>The input sample count since which the finder has seen no signal on it, or null while it does.</summary>
    internal long? AbsentSample { get; set; }

    /// <summary>The input sample count when its receiver was made.</summary>
    internal long TunedSample { get; set; }
}
