// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pota;

/// <summary>
/// The park references in the log, from ADIF <c>SIG</c>=<c>POTA</c> with <c>SIG_INFO</c>, and from <c>POTA_REF</c>
/// (ADIF 3.1.4, a comma-separated list where each reference may carry <c>@location</c>). Compared upper case, ordinally.
/// </summary>
public sealed class WorkedParks
{
    private readonly HashSet<string> _refs;

    private WorkedParks(HashSet<string> refs) => _refs = refs;

    /// <summary>No parks.</summary>
    public static WorkedParks Empty { get; } = new([]);

    /// <summary>Number of distinct parks.</summary>
    public int Count => _refs.Count;

    /// <summary>Builds the set from each contact's <c>SIG</c>, <c>SIG_INFO</c> and <c>POTA_REF</c> values (any may be null).</summary>
    public static WorkedParks Build(IEnumerable<(string? Sig, string? SigInfo, string? PotaRef)> contacts)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (sig, info, potaRef) in contacts)
        {
            if (string.Equals(sig?.Trim(), "POTA", StringComparison.OrdinalIgnoreCase)) AddList(set, info);
            AddList(set, potaRef);
        }
        return new WorkedParks(set);
    }

    /// <summary>True when the park is in the log.</summary>
    public bool Contains(string? reference) => Normalize(reference) is { } r && _refs.Contains(r);

    /// <summary>A copy with one more park (a contact just logged).</summary>
    public WorkedParks With(string reference)
    {
        if (Normalize(reference) is not { } r || _refs.Contains(r)) return this;
        return new WorkedParks(new HashSet<string>(_refs, StringComparer.Ordinal) { r });
    }

    /// <summary>A park reference trimmed and upper case, without any <c>@location</c>; null when it is not one.</summary>
    public static string? Normalize(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var r = reference.Trim().ToUpperInvariant();
        var at = r.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0) r = r[..at].Trim();
        var dash = r.IndexOf('-', StringComparison.Ordinal);
        if (dash <= 0 || dash == r.Length - 1) return null;
        return r.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-') ? r : null;
    }

    private static void AddList(HashSet<string> set, string? list)
    {
        if (string.IsNullOrWhiteSpace(list)) return;
        foreach (var part in list.Split(','))
        {
            if (Normalize(part) is { } r) set.Add(r);
        }
    }
}
