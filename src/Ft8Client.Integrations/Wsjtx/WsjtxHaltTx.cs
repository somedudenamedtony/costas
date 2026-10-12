// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>Halt Tx (type 8): stop transmitting, at once or (auto Tx only) at the end of the current transmission.</summary>
/// <param name="TargetId">The client it is meant for.</param>
/// <param name="AutoTxOnly">True to stop only after the current transmission.</param>
public sealed record WsjtxHaltTx(string TargetId, bool AutoTxOnly) : WsjtxInboundMessage(TargetId);
