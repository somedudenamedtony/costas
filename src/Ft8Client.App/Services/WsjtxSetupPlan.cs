// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;
using Ft8Client.Rig;

namespace Ft8Client.App.Services;

/// <summary>What Setup fills in from a WSJT-X configuration. Blank or null fields are left as they were.</summary>
/// <param name="Callsign">My call.</param>
/// <param name="Grid">My grid.</param>
/// <param name="AudioOnly">WSJT-X has no CAT radio ("None"): audio and VOX only.</param>
/// <param name="Model">The Hamlib model, or null when it was not found.</param>
/// <param name="Port">The CAT serial port.</param>
/// <param name="NetworkAddress">The server for a network model (FLRig, NET rigctl), host:port.</param>
/// <param name="Baud">The baud rate as Setup shows it; blank for automatic.</param>
/// <param name="Ptt">PTT method: <c>cat</c>, <c>rts</c>, <c>dtr</c>, <c>vox</c>.</param>
/// <param name="PttPort">Separate PTT port, blank for the CAT port.</param>
/// <param name="Input">The matching input device, or null.</param>
/// <param name="Output">The matching output device, or null.</param>
/// <param name="NotCopied">What could not be carried over, one line each, for the operator to set by hand.</param>
public sealed record WsjtxSetupPlan(string Callsign, string Grid, bool AudioOnly, HamlibModel? Model, string Port, string NetworkAddress, string Baud,
                                    string Ptt, string PttPort, AudioDevice? Input, AudioDevice? Output, IReadOnlyList<string> NotCopied);
