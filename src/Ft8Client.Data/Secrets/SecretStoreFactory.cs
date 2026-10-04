// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Secrets;

/// <summary>Picks the credential store for this platform.</summary>
public static class SecretStoreFactory
{
    /// <summary>Windows Credential Manager on Windows, Secret Service on Linux when available, else memory only.</summary>
    public static ISecretStore Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsCredentialStore();
        if (OperatingSystem.IsLinux() && SecretToolStore.IsAvailable()) return new SecretToolStore();
        return new InMemorySecretStore();
    }
}
