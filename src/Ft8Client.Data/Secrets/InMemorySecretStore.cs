// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.Concurrent;

namespace Ft8Client.Data.Secrets;

/// <summary>Keeps secrets for this run only. Used by tests and where no OS store is available.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool IsPersistent => false;

    /// <inheritdoc />
    public string? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;

    /// <inheritdoc />
    public void Set(string name, string value) => _values[name] = value;

    /// <inheritdoc />
    public void Delete(string name) => _values.TryRemove(name, out _);
}
