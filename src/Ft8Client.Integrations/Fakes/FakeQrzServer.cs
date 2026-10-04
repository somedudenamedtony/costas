// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Net;
using System.Text;
using Ft8Client.Data.Adif;

namespace Ft8Client.Integrations.Fakes;

/// <summary>
/// An in-process stand-in for logbook.qrz.com and xmldata.qrz.com, for tests and simulation. Reproduces the reply
/// shapes recorded in samples/qrz: HTML-entity-encoded ADIF, <c>RESULT=FAIL&amp;COUNT=0</c> for an empty page,
/// <c>RESULT=AUTH</c> for a bad key.
/// </summary>
public sealed class FakeQrzServer : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<Entry> _log = [];
    private readonly Dictionary<string, Dictionary<string, string>> _lookups = new(StringComparer.OrdinalIgnoreCase);
    private long _nextLogId = 1_000_000_000;
    private int _session;

    /// <summary>Creates a fake with a valid key and callsign.</summary>
    public FakeQrzServer(string key, string callsign)
    {
        Key = key;
        Callsign = callsign;
    }

    /// <summary>The key the fake accepts.</summary>
    public string Key { get; set; }

    /// <summary>The logbook callsign.</summary>
    public string Callsign { get; }

    /// <summary>XML username and password accepted.</summary>
    public (string User, string Password) XmlLogin { get; set; } = ("user", "pass");

    /// <summary>Fail this many next requests with HTTP 503.</summary>
    public int FailNext { get; set; }

    /// <summary>Reject inserts with this reason (e.g. outside the logbook date range).</summary>
    public string? RejectReason { get; set; }

    /// <summary>Times of every request received.</summary>
    public List<DateTime> RequestTimes { get; } = [];

    /// <summary>Every form posted, in order.</summary>
    public List<Dictionary<string, string>> Requests { get; } = [];

    /// <summary>Number of XML logins performed.</summary>
    public int XmlLogins { get; private set; }

    /// <summary>Clock for modification dates and request times.</summary>
    public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    /// <summary>Records currently in the fake logbook.</summary>
    public int Count
    {
        get { lock (_gate) return _log.Count; }
    }

    /// <summary>Adds a contact to the fake logbook and returns its log id.</summary>
    public long Add(string call, DateTime timeOnUtc, string band = "20m", string mode = "FT8", string? grid = null, bool confirmed = false, DateTime? modifiedUtc = null)
    {
        lock (_gate)
        {
            var r = new AdifRecord
            {
                ["call"] = call,
                ["qso_date"] = timeOnUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                ["time_on"] = timeOnUtc.ToString("HHmm", CultureInfo.InvariantCulture),
                ["band"] = band,
                ["mode"] = mode,
                ["gridsquare"] = grid,
                ["station_callsign"] = Callsign,
                ["app_qrzlog_status"] = confirmed ? "C" : "N",
            };
            var id = _nextLogId++;
            r["app_qrzlog_logid"] = id.ToString(CultureInfo.InvariantCulture);
            _log.Add(new Entry(id, r, modifiedUtc ?? Now()));
            return id;
        }
    }

    /// <summary>Marks a record confirmed and modified now.</summary>
    public void Confirm(long logId)
    {
        lock (_gate)
        {
            var i = _log.FindIndex(e => e.LogId == logId);
            _log[i].Record["app_qrzlog_status"] = "C";
            _log[i] = _log[i] with { Modified = Now() };
        }
    }

    /// <summary>Adds a call for XML lookups.</summary>
    public void AddLookup(string call, string? name = null, string? city = null, string? state = null, string? country = null, string? grid = null)
    {
        _lookups[call] = new Dictionary<string, string?>
        {
            ["call"] = call, ["fname"] = name?.Split(' ')[0], ["name"] = name?.Split(' ').Skip(1).FirstOrDefault(), ["addr2"] = city,
            ["state"] = state, ["country"] = country, ["grid"] = grid,
        }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value!);
    }

    /// <summary>Expires the XML session so the next lookup must log in again.</summary>
    public void ExpireSession() => _session++;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            RequestTimes.Add(Now());
            if (FailNext > 0)
            {
                FailNext--;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
        }

        if (request.Method == HttpMethod.Get) return Xml(Lookup(request.RequestUri!));

        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => p.Length > 1 ? WebUtility.UrlDecode(p[1]) : string.Empty, StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            Requests.Add(form);
            return Text(Handle(form));
        }
    }

    private string Handle(Dictionary<string, string> form)
    {
        if (!form.TryGetValue("KEY", out var key) || key != Key) return "STATUS=AUTH&RESULT=AUTH&REASON=invalid api key " + key?.Replace("-", string.Empty, StringComparison.Ordinal) + "\n&EXTENDED=";
        var action = form.GetValueOrDefault("ACTION", string.Empty).ToUpperInvariant();
        switch (action)
        {
            case "STATUS":
                var confirmed = _log.Count(e => e.Record["app_qrzlog_status"] == "C");
                return $"COUNT={_log.Count}&CONFIRMED={confirmed}&RESULT=OK&ACTION=STATUS&CALLSIGN={Callsign}&BOOK_NAME={Callsign} Logbook&DXCC_COUNT=0&OWNER={Callsign}";
            case "FETCH":
                return Fetch(form.GetValueOrDefault("OPTION", string.Empty));
            case "INSERT":
                return Insert(form.GetValueOrDefault("ADIF", string.Empty), form.GetValueOrDefault("OPTION"));
            default:
                return "RESULT=FAIL&REASON=invalid action";
        }
    }

    private string Fetch(string option)
    {
        int max = 250;
        long after = 0;
        DateTime? since = null;
        foreach (var part in option.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':', 2);
            var v = kv.Length > 1 ? kv[1] : string.Empty;
            switch (kv[0].ToUpperInvariant())
            {
                case "MAX": max = int.Parse(v, CultureInfo.InvariantCulture); break;
                case "AFTERLOGID": after = long.Parse(v, CultureInfo.InvariantCulture); break;
                case "MODSINCE": since = DateTime.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal); break;
            }
        }
        var rows = _log.Where(e => e.LogId >= after && (since is null || e.Modified >= since)).OrderBy(e => e.LogId).Take(max).ToList();
        if (rows.Count == 0) return "COUNT=0&RESULT=FAIL";
        var adif = string.Concat(rows.Select(e => AdifWriter.Record(e.Record) + "\n"));
        var encoded = adif.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
        return $"RESULT=OK&COUNT={rows.Count}&ADIF={encoded}";
    }

    private string Insert(string adif, string? option)
    {
        if (option is not null && option.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)) return "RESULT=FAIL&REASON=fake server forbids REPLACE in tests";
        if (RejectReason is not null) return $"RESULT=FAIL&REASON={RejectReason}";
        var r = AdifReader.Read(adif).FirstOrDefault();
        if (r?["call"] is null) return "RESULT=FAIL&REASON=missing call";
        var dup = _log.FirstOrDefault(e =>
            string.Equals(e.Record["call"], r["call"], StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Record["band"], r["band"], StringComparison.OrdinalIgnoreCase) &&
            e.Record["qso_date"] == r["qso_date"] &&
            e.Record["time_on"]?[..4] == r["time_on"]?[..4]);
        if (dup is not null) return "RESULT=FAIL&REASON=Unable to add QSO to database: duplicate&EXTENDED=";
        var id = _nextLogId++;
        r["app_qrzlog_logid"] = id.ToString(CultureInfo.InvariantCulture);
        r["app_qrzlog_status"] = "N";
        _log.Add(new Entry(id, r, Now()));
        return $"RESULT=OK&LOGID={id}&COUNT=1";
    }

    private string Lookup(Uri uri)
    {
        var q = uri.Query.TrimStart('?').Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : string.Empty, StringComparer.OrdinalIgnoreCase);
        const string head = "<?xml version=\"1.0\" ?><QRZDatabase version=\"1.34\" xmlns=\"http://xmldata.qrz.com\">";
        if (q.ContainsKey("username"))
        {
            if (q["username"] != XmlLogin.User || q.GetValueOrDefault("password") != XmlLogin.Password)
                return head + "<Session><Error>Username/password incorrect</Error></Session></QRZDatabase>";
            XmlLogins++;
            return head + $"<Session><Key>key{_session}</Key><Count>1</Count></Session></QRZDatabase>";
        }
        if (q.GetValueOrDefault("s") != $"key{_session}")
            return head + "<Session><Error>Session Timeout</Error></Session></QRZDatabase>";
        var call = q.GetValueOrDefault("callsign", string.Empty);
        if (!_lookups.TryGetValue(call, out var f))
            return head + $"<Session><Error>Not found: {call}</Error><Key>key{_session}</Key></Session></QRZDatabase>";
        var sb = new StringBuilder(head).Append("<Callsign>");
        foreach (var (k, v) in f) sb.Append('<').Append(k).Append('>').Append(WebUtility.HtmlEncode(v)).Append("</").Append(k).Append('>');
        return sb.Append($"</Callsign><Session><Key>key{_session}</Key></Session></QRZDatabase>").ToString();
    }

    private static HttpResponseMessage Text(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "text/plain") };

    private static HttpResponseMessage Xml(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "text/xml") };

    private sealed record Entry(long LogId, AdifRecord Record, DateTime Modified);
}
