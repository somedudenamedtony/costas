// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>A contact in Worked tonight.</summary>
/// <param name="CallWhere">"W6DTR · California, USA".</param>
/// <param name="Upload">Upload status text.</param>
/// <param name="UploadKind">Upload colour.</param>
/// <param name="Need">Need tag at the time.</param>
/// <param name="NeedKind">Need colour.</param>
/// <param name="Time">"02:49 UTC".</param>
/// <param name="QsoId">Row id, for retry.</param>
public sealed record TonightRowViewModel(string CallWhere, string Upload, TextKind UploadKind, string Need, TextKind NeedKind, string Time, long QsoId);
