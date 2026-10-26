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
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Services;

/// <summary>
/// Writes finished contacts to the local log first, then queues them for QRZ and eQSL upload when enabled. The power logged is
/// what <paramref name="radioPowerWatts"/> reports from the radio, or the power in Settings when it reports nothing. When
/// <paramref name="parkFor"/> (call, band) names a spotted POTA park, the contact gets ADIF SIG=POTA and SIG_INFO.
/// <paramref name="profileId"/> is read for each contact, so a profile switched to while running gets its own contacts.
/// With <paramref name="hold"/>, uploads wait while the operator can still correct the contact from the Logged notice.
/// </summary>
public sealed class DatabaseSessionLog(QsoRepository qsos, UploadQueueRepository queue, Func<string> profileId, Func<bool> uploadEnabled,
                                       Func<int?>? radioPowerWatts = null, Func<bool>? eqslUploadEnabled = null,
                                       Func<string, string, string?>? parkFor = null, LogHold? hold = null) : ISessionLog
{
    /// <summary>Raised after a contact is stored, with its row.</summary>
    public event Action<QsoRecord>? Stored;

    /// <inheritdoc />
    public long? Write(ContactLogEntry e, LogContext c)
    {
        var (mode, submode) = ModeInfo.Adif(c.Mode);
        var q = new QsoRecord
        {
            ProfileId = profileId(),
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
            Qth = c.Qth,
            Country = c.Country,
            EntityKey = c.EntityKey,
            Continent = c.Continent,
            StationCallsign = c.MyCall,
            MyGridsquare = c.MyGrid,
            TxPwr = (radioPowerWatts?.Invoke() ?? c.PowerWatts).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Comment = LogComment.Format(c.Mode, e.ReportSent, e.ReportReceived),
            NeedTier = c.Need.Tier,
            Source = QsoSource.Local,
        };
        if (parkFor?.Invoke(e.Call, c.Band) is { } park) PotaAdif.Tag(q, park);
        var (id, _) = qsos.Upsert(q);
        q.Id = id;
        var due = hold?.Add(id) ?? DateTime.UtcNow;
        if (uploadEnabled()) queue.Enqueue(id, "qrz", due);
        if (eqslUploadEnabled?.Invoke() == true) queue.Enqueue(id, "eqsl", due);
        Stored?.Invoke(q);
        return id;
    }
}
