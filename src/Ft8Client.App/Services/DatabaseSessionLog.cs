// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Messages;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Services;

/// <summary>Writes finished contacts to the local log first, then queues them for QRZ upload when enabled.</summary>
public sealed class DatabaseSessionLog(QsoRepository qsos, UploadQueueRepository queue, string profileId, Func<bool> uploadEnabled) : ISessionLog
{
    /// <summary>Raised after a contact is stored, with its row.</summary>
    public event Action<QsoRecord>? Stored;

    /// <inheritdoc />
    public bool Write(ContactLogEntry e, LogContext c)
    {
        var (mode, submode) = ModeInfo.Adif(c.Mode);
        var q = new QsoRecord
        {
            ProfileId = profileId,
            Call = e.Call,
            QsoDateOn = e.TimeOnUtc,
            QsoDateOff = e.TimeOffUtc,
            Band = c.Band,
            FreqHz = c.FrequencyHz,
            Mode = mode,
            Submode = submode,
            RstSent = Report.Format(e.ReportSent),
            RstRcvd = e.ReportReceived is { } r ? Report.Format(r) : null,
            Gridsquare = e.Grid,
            Name = c.Name,
            State = c.State,
            Country = c.Country,
            EntityKey = c.EntityKey,
            Continent = c.Continent,
            StationCallsign = c.MyCall,
            MyGridsquare = c.MyGrid,
            TxPwr = c.PowerWatts.ToString(System.Globalization.CultureInfo.InvariantCulture),
            NeedTier = c.Need.Tier,
            Source = QsoSource.Local,
        };
        var (id, _) = qsos.Upsert(q);
        q.Id = id;
        if (uploadEnabled()) queue.Enqueue(id, "qrz", DateTime.UtcNow);
        Stored?.Invoke(q);
        return true;
    }
}
