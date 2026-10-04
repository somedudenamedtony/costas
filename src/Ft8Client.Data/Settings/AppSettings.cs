// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>The contents of <c>settings.json</c>. Never holds secrets.</summary>
public sealed class AppSettings
{
    /// <summary>Current schema version.</summary>
    public const int CurrentSchema = 1;

    /// <summary>Schema version of the file.</summary>
    public int Schema { get; set; } = CurrentSchema;

    /// <summary>Active profile id.</summary>
    public string ActiveProfile { get; set; } = "home";

    /// <summary>Station profiles.</summary>
    public List<StationProfile> Profiles { get; set; } = [new()];

    /// <summary>Operating.</summary>
    public OperatingSettings Operating { get; set; } = new();

    /// <summary>Ranking.</summary>
    public RankingOptions Ranking { get; set; } = new();

    /// <summary>QRZ.</summary>
    public QrzOptions Qrz { get; set; } = new();

    /// <summary>PSK Reporter.</summary>
    public PskReporterOptions PskReporter { get; set; } = new();

    /// <summary>UDP.</summary>
    public UdpOptions Udp { get; set; } = new();

    /// <summary>Files.</summary>
    public FilesOptions Files { get; set; } = new();

    /// <summary>Appearance.</summary>
    public AppearanceOptions Appearance { get; set; } = new();

    /// <summary>Paths.</summary>
    public PathOptions Paths { get; set; } = new();

    /// <summary>Update checks.</summary>
    public UpdateOptions Updates { get; set; } = new();

    /// <summary>Developer mode (hidden).</summary>
    public DeveloperOptions Developer { get; set; } = new();

    /// <summary>Window.</summary>
    public WindowOptions Window { get; set; } = new();

    /// <summary>True once the first-run setup has been finished or skipped.</summary>
    public bool SetupComplete { get; set; }

    /// <summary>The active profile, created if missing.</summary>
    public StationProfile Profile
    {
        get
        {
            var p = Profiles.FirstOrDefault(x => x.Id == ActiveProfile);
            if (p is not null) return p;
            if (Profiles.Count == 0) Profiles.Add(new StationProfile());
            ActiveProfile = Profiles[0].Id;
            return Profiles[0];
        }
    }
}
