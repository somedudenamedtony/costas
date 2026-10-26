// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;
using Ft8Client.Core.Geo;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Services;

/// <summary>The log behind the Logged notice: edits are saved as the Log tab saves them, and the hold decides when uploads go.</summary>
public sealed class LoggedContactStore(QsoRepository qsos, CountryFile countries, LogHold hold, Action changed) : ILoggedContactStore
{
    /// <inheritdoc />
    public QsoRecord? Get(long qsoId) => qsos.Get(qsoId);

    /// <inheritdoc />
    public void Save(QsoRecord q)
    {
        q.EntityKey = countries.Lookup(q.Call)?.Entity.Key;
        qsos.Update(q);
        changed();
    }

    /// <inheritdoc />
    public void Keep(long qsoId, bool keep) => hold.Keep(qsoId, keep);

    /// <inheritdoc />
    public void Release(long qsoId) => hold.Release(qsoId);
}
