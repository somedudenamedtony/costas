// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>WSJT-X UDP message types this app sends (from <c>Network/NetworkMessage.hpp</c>).</summary>
public enum WsjtxMessageType : uint
{
    /// <summary>Presence, every 15 seconds.</summary>
    Heartbeat = 0,

    /// <summary>Rig and Tx state, on every change.</summary>
    Status = 1,

    /// <summary>One decode.</summary>
    Decode = 2,

    /// <summary>Earlier decodes are no longer valid.</summary>
    Clear = 3,

    /// <summary>A contact was logged.</summary>
    QsoLogged = 5,

    /// <summary>The app is closing.</summary>
    Close = 6,

    /// <summary>A contact was logged, as an ADIF document.</summary>
    LoggedAdif = 12,
}
