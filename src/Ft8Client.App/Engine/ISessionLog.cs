// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contacts;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Engine;

/// <summary>Where the session writes finished contacts (database, upload queue, interop).</summary>
public interface ISessionLog
{
    /// <summary>Writes a contact. Returns its row in the log, or null if it could not be stored.</summary>
    QsoRecord? Write(ContactLogEntry entry, LogContext context);
}
