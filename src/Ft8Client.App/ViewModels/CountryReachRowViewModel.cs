// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.App.ViewModels;

/// <summary>A row of By country: the stations in one country that heard me.</summary>
/// <param name="Country">Country name.</param>
/// <param name="Stations">Stations that heard me.</param>
/// <param name="Best">Best report text.</param>
/// <param name="BestCall">Who gave the best report.</param>
/// <param name="Farthest">Farthest distance text.</param>
/// <param name="FarthestCall">The farthest station.</param>
/// <param name="Km">Farthest distance, for sorting.</param>
public sealed record CountryReachRowViewModel(string Country, int Stations, string Best, string BestCall, string Farthest, string FarthestCall, double Km)
{
    /// <summary>Station count text.</summary>
    public string StationsText => Stations.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Hover text for the row.</summary>
    public string Tip => $"Best {Best} from {BestCall}. Farthest {FarthestCall}, {Farthest}.";
}
