// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>A caller in Waiting for you.</summary>
/// <param name="Call">Call.</param>
/// <param name="Where">Place.</param>
/// <param name="Need">Need tag.</param>
/// <param name="NeedKind">Need colour.</param>
/// <param name="Detail">"Called you at 02:54:15 · -10".</param>
/// <param name="ButtonText">"Answer" or "Answer after this contact".</param>
/// <param name="Queued">Already queued.</param>
public sealed record WaitingRowViewModel(string Call, string Where, string Need, TextKind NeedKind, string Detail, string ButtonText, bool Queued);
