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

/// <summary>A contact in Worked tonight.</summary>
/// <param name="CallWhere">"W6DTR · California, USA".</param>
/// <param name="Upload">Upload status text.</param>
/// <param name="UploadKind">Upload colour.</param>
/// <param name="Need">Need tag at the time.</param>
/// <param name="NeedKind">Need colour.</param>
/// <param name="Time">"02:49 UTC".</param>
/// <param name="QsoId">Row id, for retry.</param>
public sealed record TonightRowViewModel(string CallWhere, string Upload, TextKind UploadKind, string Need, TextKind NeedKind, string Time, long QsoId);

/// <summary>A recent report of my signal.</summary>
/// <param name="Call">Receiver.</param>
/// <param name="Where">Place.</param>
/// <param name="Db">Report.</param>
/// <param name="Age">"2 min ago".</param>
public sealed record ReportRowViewModel(string Call, string Where, string Db, string Age);

/// <summary>A past contact in the details flyout.</summary>
/// <param name="Date">Date.</param>
/// <param name="Band">Band.</param>
/// <param name="Mode">Mode.</param>
/// <param name="Confirmed">"Confirmed" or empty.</param>
public sealed record PastContactViewModel(string Date, string Band, string Mode, string Confirmed);
