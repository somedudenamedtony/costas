// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Log;

/// <summary>One row of the <c>qso</c> table. See docs/06-data-model.md.</summary>
public sealed class QsoRecord
{
    /// <summary>Row id.</summary>
    public long Id { get; set; }

    /// <summary>Station profile that made or owns it.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>DX call.</summary>
    public string Call { get; set; } = string.Empty;

    /// <summary>Time on, UTC.</summary>
    public DateTime QsoDateOn { get; set; }

    /// <summary>Time off, UTC.</summary>
    public DateTime? QsoDateOff { get; set; }

    /// <summary>ADIF band, e.g. <c>20m</c>.</summary>
    public string Band { get; set; } = string.Empty;

    /// <summary>Frequency in hertz (dial plus Tx offset).</summary>
    public long? FreqHz { get; set; }

    /// <summary>ADIF mode: <c>FT8</c>, or <c>MFSK</c> for FT4.</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>ADIF submode: <c>FT4</c> when mode is <c>MFSK</c>.</summary>
    public string? Submode { get; set; }

    /// <summary>Report sent.</summary>
    public string? RstSent { get; set; }

    /// <summary>Report received.</summary>
    public string? RstRcvd { get; set; }

    /// <summary>DX grid.</summary>
    public string? Gridsquare { get; set; }

    /// <summary>DX operator name.</summary>
    public string? Name { get; set; }

    /// <summary>DX city.</summary>
    public string? Qth { get; set; }

    /// <summary>DX US state or Canadian province.</summary>
    public string? State { get; set; }

    /// <summary>DX country name.</summary>
    public string? Country { get; set; }

    /// <summary>ADIF DXCC code.</summary>
    public int? Dxcc { get; set; }

    /// <summary>Country-file entity key, computed locally.</summary>
    public string? EntityKey { get; set; }

    /// <summary>Continent.</summary>
    public string? Continent { get; set; }

    /// <summary>My call.</summary>
    public string StationCallsign { get; set; } = string.Empty;

    /// <summary>My grid.</summary>
    public string? MyGridsquare { get; set; }

    /// <summary>Transmit power in watts.</summary>
    public string? TxPwr { get; set; }

    /// <summary>Comment.</summary>
    public string? Comment { get; set; }

    /// <summary>True if any confirmation field says so.</summary>
    public bool Confirmed { get; set; }

    /// <summary>Need tier at the time of the contact (local contacts).</summary>
    public int? NeedTier { get; set; }

    /// <summary><c>local</c>, <c>qrz</c> or <c>adif</c>.</summary>
    public string Source { get; set; } = QsoSource.Local;

    /// <summary>QRZ logbook id.</summary>
    public long? QrzLogid { get; set; }

    /// <summary><c>none</c>, <c>queued</c>, <c>uploaded</c>, <c>failed</c>.</summary>
    public string UploadState { get; set; } = Log.UploadState.None;

    /// <summary>Last upload error, verbatim.</summary>
    public string? UploadError { get; set; }

    /// <summary>The original ADIF record for imported or fetched contacts.</summary>
    public string? RawAdif { get; set; }

    /// <summary>When the row was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When the row last changed.</summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>A shallow copy.</summary>
    public QsoRecord Clone() => (QsoRecord)MemberwiseClone();
}
