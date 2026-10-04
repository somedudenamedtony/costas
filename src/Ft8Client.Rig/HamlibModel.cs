// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Rig;

/// <summary>One Hamlib rig model.</summary>
/// <param name="Number">Model number.</param>
/// <param name="Manufacturer">Manufacturer.</param>
/// <param name="Model">Model name.</param>
/// <param name="Status">Stable, Beta, Alpha, Untested.</param>
public sealed record HamlibModel(int Number, string Manufacturer, string Model, string Status)
{
    /// <summary>"Icom IC-7300".</summary>
    public string Display => $"{Manufacturer} {Model}";
}
