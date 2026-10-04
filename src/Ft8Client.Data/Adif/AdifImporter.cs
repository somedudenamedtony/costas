// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Adif;

/// <summary>Imports ADIF text into the log using the duplicate rule.</summary>
public static class AdifImporter
{
    /// <summary>Imports every record; records with no call or date are skipped and counted.</summary>
    public static AdifImportResult Import(QsoRepository repo, string text, string profileId, string stationCallsign, CountryFile countries)
    {
        var skipped = 0;
        var rows = new List<QsoRecord>();
        foreach (var r in AdifReader.Read(text))
        {
            var q = AdifMapper.ToQso(r, profileId, stationCallsign, QsoSource.Adif, countries);
            if (q is null) skipped++;
            else rows.Add(q);
        }
        var (inserted, merged) = repo.UpsertMany(rows);
        return new AdifImportResult(inserted, merged, skipped);
    }

    /// <summary>Exports contacts as an ADIF document.</summary>
    public static string Export(IEnumerable<QsoRecord> qsos, DateTime nowUtc) => AdifWriter.Document(qsos.Select(AdifMapper.ToAdif), nowUtc);
}
