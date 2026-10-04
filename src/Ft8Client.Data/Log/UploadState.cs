// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Log;

/// <summary>Upload state of a contact.</summary>
public static class UploadState
{
    /// <summary>Not to be uploaded.</summary>
    public const string None = "none";

    /// <summary>Waiting in the queue.</summary>
    public const string Queued = "queued";

    /// <summary>In the QRZ logbook.</summary>
    public const string Uploaded = "uploaded";

    /// <summary>Rejected; the error is kept.</summary>
    public const string Failed = "failed";
}
