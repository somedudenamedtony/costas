// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>What the Logged notice needs from the log: the row to edit, saving it, and holding it back from uploads.</summary>
public interface ILoggedContactStore
{
    /// <summary>The contact's row, or null if it is gone.</summary>
    QsoRecord? Get(long qsoId);

    /// <summary>Saves an edited row.</summary>
    void Save(QsoRecord q);

    /// <summary>Keeps the contact from leaving the PC (true) or starts its short hold again (false).</summary>
    void Keep(long qsoId, bool keep);

    /// <summary>Lets the contact go to QRZ, eQSL, LoTW and UDP now.</summary>
    void Release(long qsoId);
}
