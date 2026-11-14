// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Backends;

/// <summary>
/// Finds a saved audio device in the current list. Windows gives a device a new endpoint id when it comes back on
/// another USB port or after its driver reinstalls, so a device not found by id is looked for by its name.
/// </summary>
public static class AudioDeviceMatch
{
    /// <summary>The name radios' built-in USB sound cards (Yaesu, Icom, Kenwood) and many interfaces give themselves.</summary>
    public const string RadioCodecName = "USB Audio CODEC";

    /// <summary>
    /// The device with <paramref name="id"/>, else the only one named <paramref name="name"/>, else null (two devices
    /// with the same name are not guessed between).
    /// </summary>
    public static AudioDevice? Find(IReadOnlyList<AudioDevice> devices, string? id, string? name)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (id is not null && devices.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal)) is { } byId) return byId;
        if (string.IsNullOrEmpty(name)) return null;
        var byName = devices.Where(d => string.Equals(d.Name, name, StringComparison.Ordinal)).ToList();
        return byName.Count == 1 ? byName[0] : null;
    }

    /// <summary>
    /// The radio's USB sound device to offer when none is chosen yet: the only one whose name has
    /// <see cref="RadioCodecName"/> in it, else null. Only offered in Station setup, never used without the operator.
    /// </summary>
    public static AudioDevice? SuggestRadio(IReadOnlyList<AudioDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        var codecs = devices.Where(d => d.Name.Contains(RadioCodecName, StringComparison.OrdinalIgnoreCase)).ToList();
        return codecs.Count == 1 ? codecs[0] : null;
    }
}
