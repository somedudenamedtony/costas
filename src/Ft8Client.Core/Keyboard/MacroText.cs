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
/// guessed: it is reported, and the caller sends nothing until the operator fills it in or removes it. The one
/// exception is a part the macro marks optional with square brackets, such as <c>[GE {NAME}]</c>: it is left out
/// whole when one of its fields has no value.
/// </summary>
public static partial class MacroText
{
    /// <summary>The fields a macro can hold.</summary>
    public static IReadOnlyList<string> Fields { get; } = ["CALL", "NAME", "RST", "QTH", "MYCALL", "MYNAME", "MYQTH", "MYGRID", "MYRIG", "MYANT"];

    /// <summary>True when <paramref name="name"/> (without braces, any case) is one of <see cref="Fields"/>.</summary>
    public static bool IsField(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Fields.Contains(name.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// The macro's text with each field replaced by its value. <paramref name="missing"/> lists, in order, the fields
    /// with no value (left in the text as written) and any name in braces that is not a field. A part in square
    /// brackets is optional: it goes out (without the brackets) when every field in it has a value, and is left out,
    /// with the space before it, when one of its fields has none. Brackets with no field inside are plain text.
    /// </summary>
    public static string Expand(string template, IReadOnlyDictionary<string, string?> values, out IReadOnlyList<string> missing)
    {
        var segments = Segments(template, values);
        var gaps = new List<string>();
        foreach (var s in segments.Where(s => s.Missing))
        {
            var name = s.Text[1..^1].Trim().ToUpperInvariant();
            if (!gaps.Contains(name)) gaps.Add(name);
        }
        missing = gaps;
        return string.Concat(segments.Select(s => s.Text));
    }

    /// <summary>
    /// The macro's text in pieces, each field with no value (or name that is not a field) apart and marked, so the
    /// type-ahead line can show it in red. Joined, the pieces are what <see cref="Expand"/> returns: optional parts
    /// are already kept or left out.
    /// </summary>
    public static IReadOnlyList<MacroSegment> Segments(string template, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var resolved = Optional().Replace(template, m =>
        {
            var inner = m.Groups[2].Value;
            var fields = Field().Matches(inner).Select(f => f.Groups[1].Value.Trim().ToUpperInvariant()).ToList();
            // Brackets with no field in them are text like any other.
            if (fields.Count == 0) return m.Value;
            var (lead, trail) = (m.Groups[1].Value, m.Groups[3].Value);
            // A name that is not a field is never dropped quietly: the part stays so that the name is reported.
            if (fields.All(f => HasValue(f, values) || !Fields.Contains(f))) return lead + inner + trail;
            return lead.Length > 0 && trail.Length > 0 ? " " : string.Empty;
        });

        var segments = new List<MacroSegment>();
        var plain = new StringBuilder();
        var at = 0;
        foreach (Match m in Field().Matches(resolved))
        {
            plain.Append(resolved, at, m.Index - at);
            at = m.Index + m.Length;
            var name = m.Groups[1].Value.Trim().ToUpperInvariant();
            if (HasValue(name, values))
            {
                plain.Append(values[name]!.Trim());
                continue;
            }
            if (plain.Length > 0) segments.Add(new MacroSegment(plain.ToString(), false));
            plain.Clear();
            segments.Add(new MacroSegment(m.Value, true));
        }
        plain.Append(resolved, at, resolved.Length - at);
        if (plain.Length > 0) segments.Add(new MacroSegment(plain.ToString(), false));
        return segments;
    }

    /// <summary>A list of fields for a message: "{NAME} and {QTH}".</summary>
    public static string Describe(IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var sb = new StringBuilder();
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(i == fields.Count - 1 ? " and " : ", ");
            sb.Append('{').Append(fields[i]).Append('}');
        }
        return sb.ToString();
    }

    private static bool HasValue(string name, IReadOnlyDictionary<string, string?> values) =>
        Fields.Contains(name) && values.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v);

    // Anything in braces is a field reference, so a misspelt name ({MY_CALL}) is reported, never sent as written.
    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex Field();

    // An optional part, with the single space on each side of it.
    [GeneratedRegex(@"( ?)\[([^\[\]]*)\]( ?)")]
    private static partial Regex Optional();
}
