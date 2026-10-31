// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Ranking;
using Ft8Client.Integrations.Wsjtx;

namespace Ft8Client.App.ViewModels;

/// <summary>One line of Raw decodes (or a blank separator between slots).</summary>
public sealed class RawLineViewModel
{
    /// <summary>A separator with no heading.</summary>
    public static RawLineViewModel Blank { get; } = new(string.Empty, string.Empty);

    private readonly bool _separator;

    private RawLineViewModel(string heading, string summary)
    {
        _separator = true;
        Heading = heading;
        Summary = summary;
    }

    /// <summary>A slot heading: its time, then what the slot held.</summary>
    public static RawLineViewModel SlotHeading(DateTime slotUtc, IReadOnlyList<RawDecodeLine> lines, bool keyboard)
    {
        var time = DateTime.SpecifyKind(slotUtc, DateTimeKind.Utc).ToString(keyboard ? "HH:mm" : "HH:mm:ss", CultureInfo.InvariantCulture);
        var heard = lines.Where(l => !l.Transmitted).ToList();
        var parts = new List<string>();
        if (heard.Count > 0) parts.Add(keyboard ? (heard.Count == 1 ? "1 line" : $"{heard.Count} lines") : heard.Count == 1 ? "1 decode" : $"{heard.Count} decodes");
        var cq = heard.Count(l => l.IsCq);
        if (cq > 0) parts.Add($"{cq} CQ");
        var toMe = heard.Count(l => l.ToMe);
        if (toMe > 0) parts.Add($"{toMe} to you");
        if (lines.Any(l => l.Transmitted)) parts.Add("you transmitted");
        if (!keyboard && heard.Count >= 3)
        {
            var dts = heard.Select(l => l.Decode.Dt).Order().ToList();
            parts.Add("DT " + dts[dts.Count / 2].ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s");
        }
        return new RawLineViewModel(time, string.Join("  ·  ", parts));
    }

    /// <summary>Slot heading time; empty for a plain separator and for decode lines.</summary>
    public string Heading { get; } = string.Empty;

    /// <summary>Slot heading summary: decodes, CQs, lines to me, median DT.</summary>
    public string Summary { get; } = string.Empty;

    /// <summary>True for a slot heading with text.</summary>
    public bool IsHeading => _separator && Heading.Length > 0;

    /// <summary>True for a decode line (not a separator or heading).</summary>
    public bool IsLine => !_separator;

    /// <summary>Signal bar fill, 0 to 1 (-24 dB to +10 dB); 0 for my own lines.</summary>
    public double SnrFill { get; }

    /// <summary>Signal bar width in pixels, for the 28-pixel track.</summary>
    public double SnrWidth => Math.Max(2, SnrFill * 28);

    /// <summary>True when the line has a signal bar.</summary>
    public bool HasSnr { get; }

    /// <summary>True when the Why column has a tag.</summary>
    public bool HasNeed => Need.Length > 0;

    /// <summary>Builds a line.</summary>
    /// <param name="l">The decode line.</param>
    /// <param name="localZone">Zone for the Local column; the machine's zone when null.</param>
    /// <param name="highlight">Colours another program asked for this line's callsign, if any.</param>
    /// <param name="sender">What the station list knows about the sender, if anything.</param>
    /// <param name="units">Distance units.</param>
    /// <param name="partner">True when the line is from or to the station I am in a contact with.</param>
    /// <param name="keyboard">A PSK or RTTY line, which has no time offset (DT is left empty).</param>
    public RawLineViewModel(RawDecodeLine l, TimeZoneInfo? localZone = null, WsjtxHighlight? highlight = null,
                            RankedStation? sender = null, DistanceUnit units = DistanceUnit.Miles, bool partner = false, bool keyboard = false)
    {
        Background = highlight?.Background;
        Foreground = highlight?.Foreground;
        var d = l.Decode;
        var utc = DateTime.SpecifyKind(d.SlotStartUtc, DateTimeKind.Utc);
        Utc = utc.ToString("HHmmss", CultureInfo.InvariantCulture);
        Local = TimeZoneInfo.ConvertTimeFromUtc(utc, localZone ?? TimeZoneInfo.Local).ToString("HHmmss", CultureInfo.InvariantCulture);
        Db = l.Transmitted ? "Tx" : d.Snr.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        Dt = l.Transmitted || keyboard ? string.Empty : d.Dt.ToString("0.0", CultureInfo.InvariantCulture);
        Hz = d.OffsetHz.ToString(CultureInfo.InvariantCulture);
        Message = d.Text + (d.LowConfidence ? " ?" : string.Empty) + (d.APriori > 0 ? $" a{d.APriori}" : string.Empty);
        Country = l.Country ?? string.Empty;
        HasSnr = !l.Transmitted;
        SnrFill = l.Transmitted ? 0 : Math.Clamp((d.Snr + 24) / 34.0, 0, 1);
        Kind = l.Transmitted ? TextKind.Secondary : l.ToMe ? TextKind.Critical : l.IsCq ? TextKind.Accent : TextKind.Normal;
        Sender = l.Message.SenderCall;
        IsPartner = partner;
        if (sender is not null && !l.Transmitted)
        {
            Need = StationText.Why(sender.Need.Tag, sender.Station.Grid);
            NeedKind = StationRowViewModel.KindOf(sender.Need.Tag);
            Distance = sender.Station.DistanceKm is { } km ? StationText.Distance(km, units) : string.Empty;
        }
        Copy = $"{Utc} {Db,3} {Dt,4} {Hz,4} ~  {Message}";
    }

    /// <summary>Slot time.</summary>
    public string Utc { get; } = string.Empty;

    /// <summary>Slot time in the operator's local zone.</summary>
    public string Local { get; } = string.Empty;

    /// <summary>SNR.</summary>
    public string Db { get; } = string.Empty;

    /// <summary>DT.</summary>
    public string Dt { get; } = string.Empty;

    /// <summary>Offset.</summary>
    public string Hz { get; } = string.Empty;

    /// <summary>Message.</summary>
    public string Message { get; } = string.Empty;

    /// <summary>Country.</summary>
    public string Country { get; } = string.Empty;

    /// <summary>Distance to the sender, from the station list; empty when not known.</summary>
    public string Distance { get; } = string.Empty;

    /// <summary>What working the sender would add ("New country", "Worked before"); empty when not known.</summary>
    public string Need { get; } = string.Empty;

    /// <summary>Colour of <see cref="Need"/>, as in the station list.</summary>
    public TextKind NeedKind { get; } = TextKind.Secondary;

    /// <summary>True when the line is from or to the station I am in a contact with.</summary>
    public bool IsPartner { get; }

    /// <summary>The line as text for the clipboard, laid out as WSJT-X prints it.</summary>
    public string Copy { get; } = string.Empty;

    /// <summary>Colour: to me critical, CQ accent.</summary>
    public TextKind Kind { get; }

    /// <summary>Background colour from another program's highlight; null for none.</summary>
    public WsjtxColor? Background { get; }

    /// <summary>Text colour from another program's highlight; null to use <see cref="Kind"/>.</summary>
    public WsjtxColor? Foreground { get; }

    /// <summary>Sender, for double-click.</summary>
    public string? Sender { get; }

    /// <summary>True for a separator line.</summary>
    public bool IsBlank => _separator;
}
