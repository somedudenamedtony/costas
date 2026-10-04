// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Settings;

namespace Ft8Client.App.Services;

/// <summary>Holds the current settings and saves changes. Readers get a consistent object; writers replace it.</summary>
public sealed class AppSettingsAccessor(SettingsStore store, AppSettings initial)
{
    private readonly object _gate = new();
    private AppSettings _current = initial;

    /// <summary>Raised after settings are saved.</summary>
    public event Action<AppSettings>? Changed;

    /// <summary>The current settings. Do not mutate; use <see cref="Update"/>.</summary>
    public AppSettings Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Changes and saves settings.</summary>
    public void Update(Action<AppSettings> change)
    {
        AppSettings copy;
        lock (_gate)
        {
            copy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(_current, SettingsStore.Json), SettingsStore.Json)!;
            change(copy);
            store.Save(copy);
            _current = copy;
        }
        Changed?.Invoke(copy);
    }
}
