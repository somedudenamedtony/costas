// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// What is known about one station: the details flyout for the selected station, and the "Who you're calling" card in a
/// contact. Location and grid come from its messages and the country file; the rest from a QRZ lookup when there is one.
/// </summary>
public sealed class StationDetailsViewModel
{
    private StationDetailsViewModel()
    {
    }

    /// <summary>Call.</summary>
    public string Call { get; private init; } = string.Empty;

    /// <summary>Name from QRZ ("Robert “Bob” Smith"), or "—".</summary>
    public string Name { get; private init; } = "—";

    /// <summary>Place and distance.</summary>
    public string Location { get; private init; } = string.Empty;

    /// <summary>"Cook County" (US), blank when not known.</summary>
    public string County { get; private init; } = string.Empty;

    /// <summary>Grid.</summary>
    public string Grid { get; private init; } = "—";

    /// <summary>Bearing.</summary>
    public string Bearing { get; private init; } = "—";

    /// <summary>"15:42 local (UTC−5)", blank when not known.</summary>
    public string LocalTime { get; private init; } = string.Empty;

    /// <summary>"Extra class", blank when not known.</summary>
    public string LicenseClass { get; private init; } = string.Empty;

    /// <summary>"LoTW · eQSL · paper", with the QSL manager when there is one; blank when not known.</summary>
    public string Qsl { get; private init; } = string.Empty;

    /// <summary>The station's country (country-file key), for "Hunt this country"; null when not known.</summary>
    public string? EntityKey { get; private init; }

    /// <summary>"Hunt Japan", or blank when the country is not known.</summary>
    public string HuntCountryText { get; private init; } = string.Empty;

    /// <summary>The "Hunt country" link is shown.</summary>
    public bool CanHuntCountry => HuntCountryText.Length > 0;

    /// <summary>"CQ 5 · ITU 8", blank when not known.</summary>
    public string Zones { get; private init; } = string.Empty;

    /// <summary>"Rare: #61 on Club Log's most-wanted list", blank when not on the list or not loaded.</summary>
    public string Rarity { get; private init; } = string.Empty;

    /// <summary>The rarity line is shown.</summary>
    public bool HasRarity => Rarity.Length > 0;

    /// <summary>"No QRZ details" hint when nothing was looked up.</summary>
    public string LookupNote { get; private init; } = string.Empty;

    /// <summary>The station's QRZ page.</summary>
    public string QrzUrl { get; private init; } = string.Empty;

    /// <summary>Photo URL from QRZ, if any (opened in the browser).</summary>
    public string? PhotoUrl { get; private init; }

    /// <summary>A photo link is shown.</summary>
    public bool HasPhoto => PhotoUrl is not null;

    /// <summary>Visibility helpers.</summary>
    public bool HasCounty => County.Length > 0;

    /// <inheritdoc cref="HasCounty" />
    public bool HasLocalTime => LocalTime.Length > 0;

    /// <inheritdoc cref="HasCounty" />
    public bool HasLicenseClass => LicenseClass.Length > 0;

    /// <inheritdoc cref="HasCounty" />
    public bool HasQsl => Qsl.Length > 0;

    /// <inheritdoc cref="HasCounty" />
    public bool HasZones => Zones.Length > 0;

    /// <inheritdoc cref="HasCounty" />
    public bool HasLookupNote => LookupNote.Length > 0;

    /// <summary>Past contacts.</summary>
    public IReadOnlyList<PastContactViewModel> Past { get; private init; } = [];

    /// <summary>"No contacts with this call yet" when empty.</summary>
    public bool NoPast => Past.Count == 0;

    /// <summary>Pileup: "3 stations called JA6VQA at 20:29:30.", blank with no current callers.</summary>
    public string PileupHeadline { get; private init; } = string.Empty;

    /// <summary>Pileup: the current callers, strongest first.</summary>
    public IReadOnlyList<PileupCallerViewModel> PileupCallers { get; private init; } = [];

    /// <summary>Pileup: "Answered the strongest caller you heard in 4 of its last 5 contacts.", or blank.</summary>
    public string PileupPicks { get; private init; } = string.Empty;

    /// <summary>Pileup: where my offset sits among the callers, or blank.</summary>
    public string PileupOffset { get; private init; } = string.Empty;

    /// <summary>The "Who else is calling" section is shown.</summary>
    public bool HasPileup => PileupHeadline.Length > 0 || PileupPicks.Length > 0;

    /// <inheritdoc cref="HasPileup" />
    public bool HasPileupHeadline => PileupHeadline.Length > 0;

    /// <inheritdoc cref="HasPileup" />
    public bool HasPileupPicks => PileupPicks.Length > 0;

    /// <inheritdoc cref="HasPileup" />
    public bool HasPileupOffset => PileupOffset.Length > 0;

    /// <summary>Builds the details from whatever is known: the heard station, a QRZ lookup, or both.</summary>
    public static StationDetailsViewModel From(string call, Station? s, CallsignCacheEntry? q, LogIndex? log, DistanceUnit units, DateTime nowUtc,
                                               string? name = null, Pileup? pileup = null, int txOffsetHz = 0, Mode mode = Mode.Ft8, int? mostWantedRank = null,
                                               DateTime? lotwUpload = null)
    {
        var entity = s?.Entity?.Entity;
        var offset = q is null ? null : StationClock.Offset(q.GmtOffset, q.ObservesDst, s?.EntityKey, s?.Continent, nowUtc);
        return new StationDetailsViewModel
        {
            Call = call,
            Name = FullName(q?.Name ?? name, q?.Nickname) ?? "—",
            Location = s is not null ? StationText.Where(s, units) : Join(", ", q?.City, q?.State, q?.Country),
            County = string.IsNullOrWhiteSpace(q?.County) ? string.Empty : CountyText(q.County),
            Grid = s?.Grid ?? q?.Grid ?? "—",
            Bearing = s?.Bearing is { } b ? $"{b}°" : "—",
            LocalTime = StationClock.Describe(offset, nowUtc) ?? string.Empty,
            LicenseClass = LicenseText(q?.LicenseClass),
            Qsl = QslText(q, lotwUpload, nowUtc),
            EntityKey = entity?.Key,
            HuntCountryText = entity is null ? string.Empty : $"Hunt {entity.Name}",
            Rarity = mostWantedRank is { } mw ? Core.Geo.Rarity.Describe(mw) : string.Empty,
            Zones = Join(" · ", q?.CqZone is { } cq ? $"CQ {cq}" : null, q?.ItuZone is { } itu ? $"ITU {itu}" : null),
            LookupNote = q is null && name is null ? "No QRZ details yet. They appear here when QRZ lookups are set up." : string.Empty,
            QrzUrl = "https://www.qrz.com/db/" + Uri.EscapeDataString(call),
            PhotoUrl = q?.ImageUrl,
            Past = (log?.ContactsWith(call) ?? []).OrderByDescending(c => c.DateUtc)
                .Select(c => new PastContactViewModel(c.DateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), c.Band, c.ModeKey,
                    c.LotwConfirmed ? "Confirmed on LoTW" : c.Confirmed ? "Confirmed (QRZ or card)" : string.Empty)).ToList(),
            PileupHeadline = pileup is null ? string.Empty : PileupText.Headline(pileup),
            PileupCallers = pileup?.Callers.Select(c => new PileupCallerViewModel(c.Call, Report.Format(c.Snr),
                c.OffsetHz.ToString(CultureInfo.InvariantCulture) + " Hz")).ToList() ?? [],
            PileupPicks = pileup is null ? string.Empty : PileupText.Picks(pileup),
            PileupOffset = pileup is null ? string.Empty : PileupText.Offset(pileup, txOffsetHz, mode),
        };
    }

    private static string? FullName(string? name, string? nick)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.IsNullOrWhiteSpace(nick) ? null : nick.Trim();
        if (string.IsNullOrWhiteSpace(nick) || name.Contains(nick.Trim(), StringComparison.OrdinalIgnoreCase)) return name.Trim();
        return $"{name.Trim()} (“{nick.Trim()}”)";
    }

    private static string CountyText(string county) =>
        county.EndsWith("county", StringComparison.OrdinalIgnoreCase) || county.EndsWith("parish", StringComparison.OrdinalIgnoreCase)
            ? county.Trim()
            : $"{county.Trim()} County";

    // QRZ gives US classes as a letter (E, A, G, T, N); anything else is shown as given.
    private static string LicenseText(string? c) => c?.Trim().ToUpperInvariant() switch
    {
        null or "" => string.Empty,
        "E" => "Amateur Extra class",
        "A" => "Advanced class",
        "G" => "General class",
        "T" => "Technician class",
        "N" => "Novice class",
        _ => $"Licence class {c.Trim()}",
    };

    // LoTW from ARRL's user list (with the last upload) or else QRZ's flag; eQSL, paper and the manager from QRZ.
    private static string QslText(CallsignCacheEntry? q, DateTime? lotwUpload, DateTime nowUtc)
    {
        var lotw = lotwUpload is { } up && nowUtc - up <= LotwUsers.ActiveWithin
            ? $"LoTW (last upload {up.ToString("d MMM yyyy", CultureInfo.InvariantCulture)})"
            : q?.Lotw == true ? "LoTW" : null;
        if (q is null) return lotw ?? string.Empty;
        var ways = Join(" · ", lotw, q.Eqsl == true ? "eQSL" : null, q.PaperQsl == true ? "paper" : null);
        var mgr = string.IsNullOrWhiteSpace(q.QslManager) ? null : $"via {q.QslManager.Trim()}";
        return Join(" · ", ways.Length == 0 ? null : ways, mgr);
    }

    private static string Join(string sep, params string?[] parts) => string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}
