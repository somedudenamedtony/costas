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
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Services;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;

namespace Ft8Client.App.Remote;

/// <summary>Turns a session snapshot into what the phone shows, using the same wording as the Operate view.</summary>
public static class RemoteStateBuilder
{
    /// <summary>Most rows of Next in line sent to the phone.</summary>
    public const int MaxLine = 8;

    /// <summary>Most rows of Repeats and of Watching sent to the phone, as on the Operate view.</summary>
    public const int MaxRows = 8;

    /// <summary>Most band activity lines sent to the phone.</summary>
    public const int MaxDecodes = 60;

    /// <summary>Most of tonight's contacts and of who-hears-me reports sent to the phone.</summary>
    public const int MaxRecent = 20;

    /// <summary>Builds the phone's state.</summary>
    /// <param name="s">The snapshot.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="ctx">The log, QRZ, country file and bands.</param>
    /// <param name="control">"you", "desk" or "other".</param>
    /// <param name="viewing">The station this phone has open, or null.</param>
    public static RemoteState Build(SessionSnapshot s, DateTime nowUtc, RemoteContext ctx, string control, string? viewing)
    {
        var units = ctx.Units;
        var radio = s.Services.TryGetValue(ServiceNames.Radio, out var r) ? r : ServiceStatus.Off("Not started");
        var decoder = s.Services.TryGetValue(ServiceNames.Decoder, out var d) ? d : ServiceStatus.Off("Not started");
        var clock = s.ClockOffsetSeconds is { } o ? $"Clock {o.ToString("+0.0;-0.0", CultureInfo.InvariantCulture)} s" : "Clock not checked yet";
        var decoderText = decoder.Health == ServiceHealth.Ok ? "Decoder ok" : $"Decoder: {decoder.Message}";
        var inContact = s.Contact is { Outcome: ContactOutcome.InProgress };
        var log = ctx.Log();
        var band = BandPlan.Display(s.Band);

        // Same countdown as the contact card: to the slot boundary, the whole slot a transmission went out in counting as transmit.
        var slotLen = ModeInfo.SlotLength(s.Mode);
        var into = nowUtc - SlotMath.SlotStart(nowUtc, s.Mode);
        var (meter, meterTip, meterKind) = TxMeterText.Warning(s.TxMeters);
        var summary = s.Summary is null ? null : new SummaryViewModel(s.Summary, s.LogIsEmpty);

        return new RemoteState
        {
            MyCall = s.MyCall,
            Frequency = $"{(s.DialHz / 1e6).ToString("0.000", CultureInfo.InvariantCulture)} · {band} {ModeInfo.Name(s.Mode)}",
            Utc = nowUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            RadioOk = radio.Health == ServiceHealth.Ok,
            Radio = radio.Message,
            Health = $"{clock} · {decoderText}",
            Transmitting = s.Transmitting,
            OnAir = s.TransmittingMessage,
            CallingCq = s.CallingCq,
            Fault = s.Fault,
            Hunt = ctx.Hunt(),
            Control = control,
            Contact = s.Contact is null ? null : Contact(ContactPanelViewModel.From(s.Contact, s, nowUtc, units, ctx.QrzName(s.Contact.DxCall))),
            Callers = [.. s.Waiting.Select(w => Caller(w, s, inContact))],
            Line = [.. s.Rank.Line.Take(MaxLine).Select(x => Row(new StationRowViewModel(x, 0, units, nowUtc, log?.LastContactWith(x.Station.Call))))],
            Band = s.Band,
            Mode = ModeInfo.Name(s.Mode),
            Bands = [.. ctx.Bands(s.Mode).Select(b => new RemoteBand(b, BandPlan.Display(b)))],
            CanChangeBand = !s.Transmitting,
            SlotLabel = s.InTransmitSlot(nowUtc) ? "Transmitting" : "Receiving",
            SlotIntoMs = (int)Math.Clamp(into.TotalMilliseconds, 0, slotLen.TotalMilliseconds),
            SlotLengthMs = (int)slotLen.TotalMilliseconds,
            Offset = (s.TxOffsetAuto ? string.Empty : "Manual · ") + TxOffsetPicker.Describe(new TxOffsetChoice(s.TxOffsetHz, s.TxOffsetClear)),
            OffsetHz = s.TxOffsetHz,
            OffsetAuto = s.TxOffsetAuto,
            Meter = meter,
            MeterTip = meterTip,
            MeterCritical = meterKind == TextKind.Critical,
            Services = Services(s),
            Summary = summary is null ? null : new RemoteSummary(summary.Title, summary.StationsHeard, summary.Countries, summary.CallingCq, summary.YouNeed, summary.Caption),
            Repeats = [.. OperateViewModel.RepeatOrder(s.Rank.Worked).Take(MaxRows).Select(x => Repeat(new RepeatRowViewModel(x, log?.LastContactWith(x.Station.Call),
                log?.ContactsWith(x.Station.Call).Count ?? 0, units, nowUtc)))],
            Watching = [.. s.Rank.Watching.Take(MaxRows).Select(x => new WatchRowViewModel(x, nowUtc, units)).Select(x => new RemoteWatch(x.Call, x.Where, x.Why, x.Reason))],
            Signal = Signal(s, nowUtc, ctx, log, band),
            TonightTitle = $"Worked tonight · {s.Tonight.Count}",
            Tonight = [.. s.Tonight.OrderByDescending(t => t.TimeUtc).Take(MaxRecent).Select(t => new RemoteTonight(t.Call,
                StationText.Place(ctx.Countries?.Lookup(t.Call), null), BandPlan.Display(t.Band), StationText.Need(t.Need), OperateViewModel.Hm(t.TimeUtc)))],
            Logged = Logged(s.LastLogged, nowUtc),
            Decodes = [.. s.RawDecodes.Reverse().Take(MaxDecodes).Select(Decode)],
            Details = viewing is null ? null : Details(viewing, s, nowUtc, ctx, log, inContact),
        };
    }

    private static RemoteStation Row(StationRowViewModel x) =>
        new(x.Call, x.Where, x.Why, x.DoingNow, x.Db, x.CallText, x.HearsYou, x.Chance, x.LastHeard, x.LastWorked, x.ChanceReason);

    private static RemoteStation Repeat(RepeatRowViewModel x) =>
        new(x.Call, x.Where, x.Why, x.DoingNow, x.Db, x.CanCall ? x.CallText : string.Empty, x.HearsYou, x.Chance, x.LastHeard, x.LastWorked,
            x.ChanceReason);

    private static RemoteDecode Decode(RawDecodeLine l)
    {
        var x = new RawLineViewModel(l);
        var kind = x.Kind switch
        {
            TextKind.Critical => "me",
            TextKind.Accent => "cq",
            TextKind.Secondary => "tx",
            _ => string.Empty,
        };
        return new RemoteDecode(x.Utc, x.Db, x.Dt, x.Hz, x.Message, x.Country, kind, l.Transmitted ? null : x.Sender);
    }

    // QRZ, PSK Reporter and audio, only when one is not working: the phone has no room for a services panel.
    private static string Services(SessionSnapshot s)
    {
        var parts = new List<string>();
        foreach (var (key, name) in new[] { (ServiceNames.Audio, "Audio"), (ServiceNames.Qrz, "QRZ"), (ServiceNames.Eqsl, "eQSL"), (ServiceNames.Psk, "PSK Reporter") })
        {
            if (s.Services.TryGetValue(key, out var st) && st.Health is ServiceHealth.Down or ServiceHealth.Degraded) parts.Add($"{name}: {st.Message}");
        }
        return string.Join(" · ", parts);
    }

    // Same figures as "Your signal" on the Operate view.
    private static RemoteLogged? Logged(LoggedContact? c, DateTime now)
    {
        var left = c is null ? TimeSpan.Zero : c.LoggedUtc + LoggedNoticeViewModel.ShowFor - now;
        return c is null || left <= TimeSpan.Zero ? null
            : new RemoteLogged(c.Id, c.Call, StationText.Need(c.Need), string.Join(" · ", new[] { LoggedNoticeViewModel.ReportsOf(c, withTime: false), c.Place,
                LoggedNoticeViewModel.PowerOf(c) }
                .Where(x => x.Length > 0)), (int)left.TotalMilliseconds);
    }

    private static RemoteSignal Signal(SessionSnapshot s, DateTime now, RemoteContext ctx, LogIndex? log, string band)
    {
        var psk = s.Services.TryGetValue(ServiceNames.Psk, out var p) ? p : null;
        var source = psk?.Health switch
        {
            ServiceHealth.Ok => "PSK Reporter · live",
            ServiceHealth.Degraded => $"PSK Reporter · updated {StationText.ShortAgo(now - psk.SinceUtc)}",
            _ => "PSK Reporter · unavailable",
        };
        var points = ctx.Countries is null
            ? []
            : ReachPoint.From(s.Reports, now, TimeSpan.FromMinutes(15), s.MyGrid, ctx.Countries, log ?? LogIndex.Empty, s.Band, ModeInfo.Name(s.Mode));
        var countries = points.Select(x => x.EntityKey).Where(k => k is not null).Distinct().Count();
        var far = points.MaxBy(x => x.Km);
        var best = points.MaxBy(x => x.Snr);
        return new RemoteSignal($"Your signal on {band}",
            points.Count == 0 ? "No reports yet. Reports appear a few minutes after you transmit." : $"{points.Count} stations in {countries} countries heard you in the last 15 minutes.",
            source,
            far is null ? "—" : $"{far.Call} · {StationText.Distance(far.Km, ctx.Units)}",
            best is null ? "—" : $"{Report.Format(best.Snr)} from {best.Call}",
            NeighbourText.Headline(s.Neighbours),
            [.. points.OrderByDescending(x => x.TimeUtc).Take(MaxRecent).Select(x => new RemoteReport(x.Call, x.Where, Report.Format(x.Snr), StationText.ShortAgo(now - x.TimeUtc)))]) { History = s.ReceptionHistory };
    }

    // The Operate view's details flyout, plus a button that does what Call or Answer would do for this station there.
    private static RemoteDetails Details(string call, SessionSnapshot s, DateTime now, RemoteContext ctx, LogIndex? log, bool inContact)
    {
        var station = s.Stations.FirstOrDefault(x => Callsign.EqualsCall(x.Call, call));
        var v = StationDetailsViewModel.From(call, station, ctx.QrzLookup(call), log, ctx.Units, now, ctx.QrzName(call),
            s.Pileups.GetValueOrDefault(Callsign.Normalize(call)), s.TxOffsetHz, s.Mode, ctx.MostWantedRank(station?.EntityKey));
        var ranked = s.Rank.Line.Concat(s.Rank.CallingMe).Concat(s.Rank.Worked).Concat(s.Rank.Watching)
            .FirstOrDefault(x => Callsign.EqualsCall(x.Station.Call, call));
        var waiting = s.Waiting.FirstOrDefault(w => Callsign.EqualsCall(w.Call, call));
        var caller = waiting is null ? null : Caller(waiting, s, inContact);
        var inThisContact = inContact && Callsign.EqualsCall(s.Contact!.DxCall, call);
        var (button, command) = inThisContact ? (string.Empty, "call")
            : caller is not null ? (caller.Button, caller.Command)
            : ranked is { Callable: true } ? ("Call", "call")
            : (string.Empty, "call");
        return new RemoteDetails
        {
            Call = v.Call,
            Name = v.Name,
            Location = v.Location,
            County = v.County,
            Grid = v.Grid,
            Bearing = v.Bearing,
            LocalTime = v.LocalTime,
            LicenseClass = v.LicenseClass,
            Qsl = v.Qsl,
            Zones = v.Zones,
            Rarity = v.Rarity,
            LookupNote = v.LookupNote,
            Need = ranked is null ? string.Empty : StationText.Need(ranked.Need.Tag),
            Heard = station is null ? string.Empty
                : $"{StationText.DoingNow(station)} · {Report.Format(station.LastSnr)} dB · {StationText.Ago(now - station.LastHeardEndUtc)}",
            Pileup = v.PileupHeadline,
            Past = v.Past,
            Button = button,
            Command = command,
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
