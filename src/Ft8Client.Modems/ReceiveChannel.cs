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
    }

    /// <summary>The channel's number, stable while it lives.</summary>
    public int Id { get; }

    /// <summary>Its receiver.</summary>
    public IKeyboardReceiver Receiver { get; }

    /// <summary>Kept even when quiet (the operator chose it).</summary>
    public bool Pinned { get; internal set; }

    /// <summary>The input sample count when it last produced text.</summary>
    internal long LastTextSample { get; set; }
}
