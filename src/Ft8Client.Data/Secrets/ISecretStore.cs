// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Secrets;

/// <summary>Stores secrets in the operating system's credential store. Never in settings, logs or the repository.</summary>
public interface ISecretStore
{
    /// <summary>True when secrets survive a restart.</summary>
    bool IsPersistent { get; }

    /// <summary>Reads a secret, or null.</summary>
    string? Get(string name);

    /// <summary>Writes a secret.</summary>
    void Set(string name, string value);

    /// <summary>Deletes a secret if present.</summary>
    void Delete(string name);
}
