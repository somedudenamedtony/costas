// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.ViewModels;

/// <summary>One message in the contest sheet's preview.</summary>
/// <param name="When">When it is sent.</param>
/// <param name="Text">The message.</param>
public sealed record ContestPreviewLine(string When, string Text);
