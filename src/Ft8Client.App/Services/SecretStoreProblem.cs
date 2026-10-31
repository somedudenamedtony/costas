// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Services;

/// <summary>What Settings and Setup say when the system keyring will not keep a key or password.</summary>
public static class SecretStoreProblem
{
    /// <summary>The message for a refused store, with the keyring's own reason.</summary>
    public static string Describe(string reason) =>
        $"Everything else is saved, but the system keyring would not keep your keys and passwords ({reason.TrimEnd('.')})."
        + (OperatingSystem.IsWindows()
            ? " Try again; if it keeps failing, check Windows Credential Manager."
            : " Costas keeps them in the desktop keyring: install and unlock one (GNOME Keyring or KWallet), then try again.");
}
