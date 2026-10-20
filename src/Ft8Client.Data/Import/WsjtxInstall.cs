// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Import;

/// <summary>
/// Where WSJT-X keeps its settings and log on this computer. WSJT-X uses Qt's standard locations with the application
/// name "WSJT-X" (<c>MultiSettings.cpp</c>, <c>logbook/WorkedBefore.cpp</c>): on Windows both files are in
/// <c>%LOCALAPPDATA%\WSJT-X</c>; on Linux the settings are <c>~/.config/WSJT-X.ini</c> and the log is
/// <c>~/.local/share/WSJT-X/wsjtx_log.adi</c>.
/// </summary>
/// <param name="SettingsPath">The settings file, or null if there is none.</param>
/// <param name="LogPath">The ADIF log, or null if there is none.</param>
public sealed record WsjtxInstall(string? SettingsPath, string? LogPath)
{
    /// <summary>The settings file name.</summary>
    public const string SettingsFile = "WSJT-X.ini";

    /// <summary>The log file name.</summary>
    public const string LogFile = "wsjtx_log.adi";

    /// <summary>Looks for WSJT-X's files under the given folders. Null when neither exists.</summary>
    /// <param name="windows">True for Windows locations.</param>
    /// <param name="localAppData">Windows: <c>%LOCALAPPDATA%</c>.</param>
    /// <param name="configHome">Linux: the configuration folder (<c>$XDG_CONFIG_HOME</c>, else <c>~/.config</c>).</param>
    /// <param name="dataHome">Linux: the data folder (<c>$XDG_DATA_HOME</c>, else <c>~/.local/share</c>).</param>
    public static WsjtxInstall? Find(bool windows, string? localAppData, string? configHome, string? dataHome)
    {
        string? settings = null, log = null;
        if (windows && !string.IsNullOrEmpty(localAppData))
        {
            settings = Path.Combine(localAppData, "WSJT-X", SettingsFile);
            log = Path.Combine(localAppData, "WSJT-X", LogFile);
        }
        else if (!windows)
        {
            if (!string.IsNullOrEmpty(configHome)) settings = Path.Combine(configHome, SettingsFile);
            if (!string.IsNullOrEmpty(dataHome)) log = Path.Combine(dataHome, "WSJT-X", LogFile);
        }
        var found = new WsjtxInstall(File.Exists(settings) ? settings : null, File.Exists(log) ? log : null);
        return found.SettingsPath is null && found.LogPath is null ? null : found;
    }

    /// <summary>Looks for WSJT-X's files for the current user on this operating system.</summary>
    public static WsjtxInstall? FindForCurrentUser()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? Xdg(string variable, params string[] fallback) =>
            Environment.GetEnvironmentVariable(variable) is { Length: > 0 } v ? v
            : home.Length > 0 ? Path.Combine([home, .. fallback]) : null;
        return Find(OperatingSystem.IsWindows(), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Xdg("XDG_CONFIG_HOME", ".config"), Xdg("XDG_DATA_HOME", ".local", "share"));
    }

    /// <summary>The settings, or null when there is no settings file or it cannot be read.</summary>
    public WsjtxSettings? ReadSettings()
    {
        if (SettingsPath is null) return null;
        try
        {
            return WsjtxSettingsReader.Parse(File.ReadAllText(SettingsPath));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
