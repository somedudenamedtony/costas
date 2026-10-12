// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;

namespace Ft8Client.App.Remote;

/// <summary>Turns a session snapshot into what the phone shows, using the same wording as the Operate view.</summary>
public static class RemoteStateBuilder
{
    /// <summary>Most rows of Next in line sent to the phone.</summary>
    public const int MaxLine = 8;

    /// <summary>Builds the phone's state.</summary>
    /// <param name="s">The snapshot.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="units">Distance units.</param>
    /// <param name="qrzName">Name from a QRZ lookup, by call.</param>
    /// <param name="control">"you", "desk" or "other".</param>
    public static RemoteState Build(SessionSnapshot s, DateTime nowUtc, DistanceUnit units, Func<string, string?> qrzName, string control)
    {
        var radio = s.Services.TryGetValue(ServiceNames.Radio, out var r) ? r : ServiceStatus.Off("Not started");
        var decoder = s.Services.TryGetValue(ServiceNames.Decoder, out var d) ? d : ServiceStatus.Off("Not started");
        var clock = s.ClockOffsetSeconds is { } o ? $"Clock {o.ToString("+0.0;-0.0", CultureInfo.InvariantCulture)} s" : "Clock not checked yet";
        var decoderText = decoder.Health == ServiceHealth.Ok ? "Decoder ok" : $"Decoder: {decoder.Message}";
        var inContact = s.Contact is { Outcome: ContactOutcome.InProgress };
        return new RemoteState
        {
            MyCall = s.MyCall,
            Frequency = $"{(s.DialHz / 1e6).ToString("0.000", CultureInfo.InvariantCulture)} · {BandPlan.Display(s.Band)} {ModeInfo.Name(s.Mode)}",
            Utc = nowUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            RadioOk = radio.Health == ServiceHealth.Ok,
            Radio = radio.Message,
            Health = $"{clock} · {decoderText}",
            Transmitting = s.Transmitting,
            OnAir = s.TransmittingMessage,
            CallingCq = s.CallingCq,
            Fault = s.Fault,
            Control = control,
            Contact = s.Contact is null ? null : Contact(ContactPanelViewModel.From(s.Contact, s, nowUtc, units, qrzName(s.Contact.DxCall))),
            Callers = [.. s.Waiting.Select(w => Caller(w, s, inContact))],
            Line = [.. s.Rank.Line.Take(MaxLine).Select(x => new RemoteStation(x.Station.Call, StationText.Where(x.Station, units), StationText.Need(x.Need.Tag),
                StationText.DoingNow(x.Station), Report.Format(x.Station.LastSnr), x.CallingMe ? "Answer" : "Call"))],
        };
    }

    private static RemoteContact Contact(ContactPanelViewModel c) => new(c.Caption, c.Call, c.Need, c.Subline, c.Sent, c.Received, c.Active, c.CanLogNow,
        [.. c.Steps.Select(x => new RemoteStep(x.Index, x.Mine, x.Who, x.Time, x.Message, x.Status, Look(x.Look), x.CanJump))]);

    private static string Look(StepLook look) => look switch
    {
        StepLook.Done => "done",
        StepLook.Transmitting => "sending",
        StepLook.Waiting => "waiting",
        _ => "upcoming",
    };

    // Same wording as the Calling you bar on the Operate view.
    private static RemoteCaller Caller(WaitingCaller w, SessionSnapshot s, bool inContact)
    {
        var st = s.Stations.FirstOrDefault(x => Callsign.EqualsCall(x.Call, w.Call));
        var need = s.Rank.CallingMe.FirstOrDefault(x => Callsign.EqualsCall(x.Station.Call, w.Call))?.Need.Tag;
        var verb = w.AfterStop ? "Resume" : "Answer";
        var line = w.AfterStop
            ? $"Last heard at {w.LastCalledUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} · {Report.Format(w.Snr)}"
            : $"Called you at {w.FirstCalledUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} · {Report.Format(w.Snr)}";
        var button = inContact ? (w.AnswerAfter ? $"Will {verb.ToLowerInvariant()} after this contact" : $"{verb} after this contact") : verb;
        return new RemoteCaller(w.Call, st is null ? string.Empty : StationText.Place(st.Entity, st.Region, st.City), need is { } n ? StationText.Need(n) : string.Empty,
            line, button, inContact ? "answerAfter" : "answer", w.AnswerAfter, w.AfterStop ? $"Replied after you stopped: {w.LastMessage}" : string.Empty);
    }
}
