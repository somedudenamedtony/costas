// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;

namespace Ft8Client.Core;

/// <summary>Product identity. The product name is not chosen yet; change it here only.</summary>
public static class AppInfo
{
    /// <summary>Display name of the product.</summary>
    public const string ProductName = "FT8 Client";

    /// <summary>Identifier used in User-Agent headers, UDP ids and folder names.</summary>
    public const string Id = "Ft8Client";

    /// <summary>Informational version of the running build.</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    /// <summary>The User-Agent required by QRZ and used for all HTTP requests.</summary>
    public static string UserAgent(string callsign) =>
        string.IsNullOrWhiteSpace(callsign) ? $"{Id}/{Version}" : $"{Id}/{Version} ({callsign.Trim().ToUpperInvariant()})";
}
