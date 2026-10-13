// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Secrets;

/// <summary>Names of the secrets the app keeps.</summary>
public static class SecretNames
{
    /// <summary>Prefix for every credential the app writes.</summary>
    public const string Prefix = "Costas:";

    /// <summary>Prefix used before the rename to Costas.</summary>
    public const string LegacyPrefix = "Ft8Client:";

    /// <summary>Every name the app uses for these station profiles.</summary>
    public static IEnumerable<string> All(IEnumerable<string> profileIds) =>
        profileIds.SelectMany(id => new[] { QrzLogbookKey(id), EqslUsername(id), EqslPassword(id) }).Append(QrzUsername).Append(QrzPassword);

    /// <summary>QRZ logbook API key for a station profile (a logbook serves one callsign).</summary>
    public static string QrzLogbookKey(string profileId) => $"{Prefix}qrz-logbook-key:{profileId}";

    /// <summary>eQSL username for a station profile.</summary>
    public static string EqslUsername(string profileId) => $"{Prefix}eqsl-username:{profileId}";

    /// <summary>eQSL password for a station profile.</summary>
    public static string EqslPassword(string profileId) => $"{Prefix}eqsl-password:{profileId}";

    /// <summary>QRZ XML username.</summary>
    public const string QrzUsername = Prefix + "qrz-username";

    /// <summary>QRZ XML password.</summary>
    public const string QrzPassword = Prefix + "qrz-password";

    /// <summary>Hashes of the tokens of phones paired for remote control.</summary>
    public const string RemoteDevices = Prefix + "remote-devices";
}
