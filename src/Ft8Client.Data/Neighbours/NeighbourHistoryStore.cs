// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Text.Json;
using Ft8Client.Core.Neighbours;

namespace Ft8Client.Data.Neighbours;

/// <summary>
/// Keeps the daily neighbour history in a small JSON file. A missing file is an empty history; an unreadable one is
/// kept as <c>.bad</c> and the history starts again. Writes go to a temporary file that then replaces the old one.
/// </summary>
public sealed class NeighbourHistoryStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        IgnoreReadOnlyProperties = true,
    };

    private readonly Lock _write = new();

    /// <summary>The file path.</summary>
    public string Path { get; } = path;

    /// <summary>Loads the history; empty when the file is missing or unreadable.</summary>
    public NeighbourHistory Load()
    {
        string text;
        try
        {
            if (!File.Exists(Path)) return NeighbourHistory.Empty;
            text = File.ReadAllText(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return NeighbourHistory.Empty;
        }
        try
        {
            var file = JsonSerializer.Deserialize<NeighbourHistoryFile>(text, Json);
            if (file is null) return NeighbourHistory.Empty;
            // A hand edit or a half-written entry is skipped rather than failing the whole file.
            var days = (file.Days ?? []).Where(d => d is { Call: not null, Band: not null, Mode: not null, Continent: not null, DiffCounts: not null })
                .Where(d => d.DiffCounts.Values.All(n => n >= 0)).ToList();
            var marks = (file.Marks ?? []).Where(m => m is { Call: not null }).Select(m => m.Note is null ? m with { Note = string.Empty } : m).ToList();
            return new NeighbourHistory(days, marks);
        }
        catch (JsonException)
        {
            try
            {
                File.Copy(Path, Path + ".bad", overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keeping the bad copy is a courtesy; starting again does not depend on it.
            }
            return NeighbourHistory.Empty;
        }
    }

    /// <summary>Saves the history, replacing the file in one step.</summary>
    public void Save(NeighbourHistory history)
    {
        var file = new NeighbourHistoryFile(NeighbourHistoryFile.CurrentSchema, history.Days.ToList(), history.Marks.ToList());
        var text = JsonSerializer.Serialize(file, Json);
        lock (_write)
        {
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
            if (dir is not null) Directory.CreateDirectory(dir);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, Path, overwrite: true);
        }
    }
}
