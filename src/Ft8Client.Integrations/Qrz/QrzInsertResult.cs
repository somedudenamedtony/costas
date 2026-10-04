// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>The result of an INSERT.</summary>
/// <param name="Outcome">Outcome.</param>
/// <param name="LogId">QRZ log id when inserted.</param>
/// <param name="Reason">QRZ's reason, verbatim.</param>
public sealed record QrzInsertResult(QrzInsertOutcome Outcome, long? LogId, string? Reason);
