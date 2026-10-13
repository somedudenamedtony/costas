// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>Reply (type 4): act as if the operator double-clicked this decode.</summary>
/// <param name="TargetId">The client it is meant for.</param>
/// <param name="TimeOfDay">The decode's slot time.</param>
/// <param name="Snr">SNR.</param>
/// <param name="Dt">Time offset.</param>
/// <param name="OffsetHz">Audio offset.</param>
/// <param name="Mode">Mode character as in the decode.</param>
/// <param name="Message">The decoded message text.</param>
/// <param name="LowConfidence">The low-confidence flag.</param>
/// <param name="Modifiers">Keyboard modifiers "as if" held; ignored by this app.</param>
public sealed record WsjtxReply(string TargetId, TimeSpan TimeOfDay, int Snr, double Dt, uint OffsetHz, string Mode, string Message,
    bool LowConfidence, byte Modifiers) : WsjtxInboundMessage(TargetId);
