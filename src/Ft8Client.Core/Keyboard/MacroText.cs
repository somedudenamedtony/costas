// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text;
using System.Text.RegularExpressions;

namespace Ft8Client.Core.Keyboard;

/// <summary>
/// Fills a macro's fields (docs/09-keyboard-modes.md section 8). A field with no value is never sent blank or
/// guessed: it is reported, and the caller sends nothing until the operator fills it in or removes it.
/// </summary>
public static partial class MacroText
{
    /// <summary>The fields a macro can hold.</summary>
    public static IReadOnlyList<string> Fields { get; } = ["CALL", "NAME", "RST", "QTH", "MYCALL", "MYNAME", "MYQTH", "MYGRID", "MYRIG", "MYANT"];

    /// <summary>
    /// The macro's text with each field replaced by its value. <paramref name="missing"/> lists, in order, the fields
    /// with no value (left in the text as written) and any name that is not a field.
    /// </summary>
    public static string Expand(string template, IReadOnlyDictionary<string, string?> values, out IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var gaps = new List<string>();
        var text = Field().Replace(template, m =>
        {
            var name = m.Groups[1].Value.ToUpperInvariant();
            if (Fields.Contains(name) && values.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            if (!gaps.Contains(name)) gaps.Add(name);
            return m.Value;
        });
        missing = gaps;
        return text;
    }

    /// <summary>A list of fields for a message: "{NAME} and {QTH}".</summary>
    public static string Describe(IReadOnlyList<string> fields)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(i == fields.Count - 1 ? " and " : ", ");
            sb.Append('{').Append(fields[i]).Append('}');
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Field();
}
