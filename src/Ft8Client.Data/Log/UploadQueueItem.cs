// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Log;

/// <summary>One row of <c>upload_queue</c>.</summary>
public sealed class UploadQueueItem
{
    /// <summary>Row id.</summary>
    public long Id { get; set; }

    /// <summary>The contact to upload.</summary>
    public long QsoId { get; set; }

    /// <summary>Upload target; <c>qrz</c>.</summary>
    public string Target { get; set; } = "qrz";

    /// <summary>Attempts so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Earliest time for the next attempt.</summary>
    public DateTime NextTryUtc { get; set; }

    /// <summary>Last error, verbatim.</summary>
    public string? LastError { get; set; }
}
