// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Updates;

/// <summary>A published build.</summary>
/// <param name="Version">Build version, from the release tag (<c>v0.1.0.123</c>).</param>
/// <param name="Tag">Release tag.</param>
/// <param name="PageUrl">Release page, for the release notes.</param>
/// <param name="InstallerName">Installer file name.</param>
/// <param name="InstallerUrl">Installer download URL.</param>
/// <param name="InstallerSize">Installer size in bytes.</param>
/// <param name="Sha256">Installer SHA-256 (lower-case hex), or null when the release does not publish one.</param>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, string InstallerName, string InstallerUrl, long InstallerSize, string? Sha256);
