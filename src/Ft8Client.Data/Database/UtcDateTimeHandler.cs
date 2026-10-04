// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Data;
using System.Globalization;
using Dapper;

namespace Ft8Client.Data.Database;

/// <summary>Stores <see cref="DateTime"/> as UTC ISO 8601 text and reads it back as UTC.</summary>
internal sealed class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = ToText(value);
    }

    public override DateTime Parse(object value) => FromText(Convert.ToString(value, CultureInfo.InvariantCulture)!);

    public static string ToText(DateTime value) =>
        (value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc))
        .ToString(Format, CultureInfo.InvariantCulture);

    public static DateTime FromText(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
