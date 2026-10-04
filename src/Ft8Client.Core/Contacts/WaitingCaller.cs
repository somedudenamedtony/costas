// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Contacts;

/// <summary>A station that called me and has not been answered.</summary>
/// <param name="Call">Its call.</param>
/// <param name="Grid">Its grid, if it sent one.</param>
/// <param name="Snr">Its SNR at my station when it called.</param>
/// <param name="Kind">What it sent (grid reply or report).</param>
/// <param name="Report">The report it sent, if any.</param>
/// <param name="Parity">Its slot parity.</param>
/// <param name="OffsetHz">Its audio offset.</param>
/// <param name="FirstCalledUtc">When it first called.</param>
/// <param name="LastCalledUtc">When it last called.</param>
/// <param name="AnswerAfter">The operator asked to answer it after the current contact.</param>
public sealed record WaitingCaller(string Call, string? Grid, int Snr, MessageKind Kind, int? Report, SlotParity Parity, int OffsetHz,
                                   DateTime FirstCalledUtc, DateTime LastCalledUtc, bool AnswerAfter = false);
