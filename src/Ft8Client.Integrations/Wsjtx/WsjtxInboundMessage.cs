// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>A datagram another program sent to us (WSJT-X protocol, "In" messages) that this app acts on.</summary>
/// <param name="TargetId">The Id field: the client the message is meant for.</param>
public abstract record WsjtxInboundMessage(string TargetId);
