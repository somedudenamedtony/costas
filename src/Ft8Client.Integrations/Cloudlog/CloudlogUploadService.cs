// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.Integrations.Cloudlog;

/// <summary>
/// Drains the Wavelog/Cloudlog part of the persistent upload queue (owner request 2026-11-07, from the Aurora
/// comparison). Failures stay queued: a network fault retries with the usual back-off, a wrong key or station id
/// waits an hour, so a bad setting does not hammer the site.
/// </summary>
public sealed class CloudlogUploadService(CloudlogClient client, QsoRepository qsos, UploadQueueRepository queue, IClock clock,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    /// <summary>Persistent queue target.</summary>
    public const string Target = "cloudlog";

    /// <summary>Wait after a refusal that retrying soon cannot fix.</summary>
    public static readonly TimeSpan PermanentRetry = TimeSpan.FromHours(1);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly Backoff _backoff = Backoff.Network();
    private DateTime _lastRequest = DateTime.MinValue;

    /// <summary>Visible service status after the last run.</summary>
    public ServiceStatus Status { get; private set; } = ServiceStatus.Off("Wavelog/Cloudlog not set up");

    /// <summary>Uploads the due contacts of this profile and callsign, one a second, stopping at the first failure.</summary>
    public async Task<int> ProcessDueAsync(string profileId, string callsign, Uri site, string key, string stationId, CancellationToken ct)
    {
        var done = 0;
        foreach (var item in queue.All().Where(i => i.Target == Target && i.NextTryUtc <= clock.UtcNow))
        {
            ct.ThrowIfCancellationRequested();
            var q = qsos.Get(item.QsoId);
            if (q is null) { queue.Remove(item.Id); continue; }
            if (q.ProfileId != profileId || !string.Equals(q.StationCallsign, callsign, StringComparison.OrdinalIgnoreCase)) continue;
            var wait = _lastRequest + TimeSpan.FromSeconds(1) - clock.UtcNow;
            if (wait > TimeSpan.Zero) await _delay(wait, ct).ConfigureAwait(false);
            _lastRequest = clock.UtcNow;
            var result = await client.UploadAsync(site, key, stationId, AdifMapper.ToAdif(q), ct).ConfigureAwait(false);
            if (result.Accepted)
            {
                queue.Remove(item.Id);
                Status = new(ServiceHealth.Ok, $"Uploaded {q.Call} to {site.Host}", clock.UtcNow);
                done++;
                continue;
            }
            var next = clock.UtcNow + (result.Permanent ? PermanentRetry : _backoff.DelayFor(item.Attempts + 1));
            queue.Retry(item.Id, result.Error ?? "Upload failed", next);
            Status = new(result.Permanent ? ServiceHealth.Down : ServiceHealth.Degraded, result.Error ?? "Waiting to retry", clock.UtcNow);
            break;
        }
        return done;
    }
}
