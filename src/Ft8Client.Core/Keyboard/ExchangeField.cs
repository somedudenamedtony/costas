// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Core.Keyboard;

/// <summary>A part of the exchange the operator can set by clicking a word in the conversation.</summary>
public enum ExchangeField
{
    /// <summary>The partner's call.</summary>
    Call,

    /// <summary>Their name.</summary>
    Name,

    /// <summary>Where they are.</summary>
    Qth,

    /// <summary>The report they sent me.</summary>
    Rst,

    /// <summary>Their grid.</summary>
    Grid,
}
