// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Adif;

/// <summary>One ADIF record: field names (lower case) to values, in the order read or added.</summary>
public sealed class AdifRecord
{
    private readonly List<KeyValuePair<string, string>> _fields = [];

    /// <summary>Fields in order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Fields => _fields;

    /// <summary>Gets a field value, or null when absent or empty.</summary>
    public string? this[string name]
    {
        get
        {
            foreach (var kv in _fields)
            {
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv.Value.Length == 0 ? null : kv.Value;
            }
            return null;
        }
        set
        {
            var key = name.ToLowerInvariant();
            var i = _fields.FindIndex(kv => kv.Key == key);
            if (value is null)
            {
                if (i >= 0) _fields.RemoveAt(i);
                return;
            }
            if (i >= 0) _fields[i] = new(key, value);
            else _fields.Add(new(key, value));
        }
    }
}
