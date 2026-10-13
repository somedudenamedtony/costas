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

namespace Ft8Client.Integrations.Eqsl;

/// <summary>Drains eQSL's persistent queue, leaving failures due for a later retry.</summary>
public sealed class EqslUploadService(EqslClient client, QsoRepository qsos, UploadQueueRepository queue, IClock clock,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    /// <summary>Persistent queue target.</summary>
    public const string Target = "eqsl";
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly Backoff _backoff = Backoff.Network();
    private DateTime _lastRequest = DateTime.MinValue;

    /// <summary>Visible service status.</summary>
    public ServiceStatus Status { get; private set; } = ServiceStatus.Off("eQSL not set up");

    /// <summary>Processes only contacts belonging to this profile and callsign.</summary>
    public async Task<int> ProcessDueAsync(string profileId, string callsign, string user, string password, string nickname, CancellationToken ct)
    {
        var done = 0;
        // Filter before limiting so another profile cannot starve the active station's queue.
        foreach (var item in queue.All().Where(i => i.Target == Target && i.NextTryUtc <= clock.UtcNow))
        {
            ct.ThrowIfCancellationRequested();
            var q = qsos.Get(item.QsoId);
            if (q is null) { queue.Remove(item.Id); continue; }
            if (q.ProfileId != profileId || !string.Equals(q.StationCallsign, callsign, StringComparison.OrdinalIgnoreCase)) continue;
            var wait = _lastRequest + TimeSpan.FromSeconds(1) - clock.UtcNow;
            if (wait > TimeSpan.Zero) await _delay(wait, ct).ConfigureAwait(false);
            _lastRequest = clock.UtcNow;
            var result = await client.UploadAsync(user, password, nickname, AdifMapper.ToAdif(q), ct).ConfigureAwait(false);
            if (result.Accepted)
            {
                qsos.SetEqslUploadState(q.Id, UploadState.Uploaded, null);
                queue.Remove(item.Id);
                Status = new(ServiceHealth.Ok, "Uploaded to eQSL", clock.UtcNow);
                done++;
            }
            else
            {
                qsos.SetEqslUploadState(q.Id, UploadState.Queued, result.Error);
                queue.Retry(item.Id, result.Error ?? "eQSL upload failed", clock.UtcNow + _backoff.DelayFor(item.Attempts + 1));
                Status = new(ServiceHealth.Degraded, result.Error ?? "eQSL waiting to retry", clock.UtcNow);
                break;
            }
        }
        return done;
    }
}
