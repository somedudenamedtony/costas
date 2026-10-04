// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.Integrations.Qrz;

/// <summary>
/// Works the persistent upload queue: one INSERT at a time, at most one request per second, back-off on failure,
/// duplicates count as uploaded, rejections keep QRZ's reason verbatim. Never sends REPLACE.
/// </summary>
public sealed class QrzUploadService
{
    /// <summary>Upload target name.</summary>
    public const string Target = "qrz";

    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);
    private readonly QrzLogbookClient _client;
    private readonly QsoRepository _qsos;
    private readonly UploadQueueRepository _queue;
    private readonly IClock _clock;
    private readonly Backoff _backoff;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    /// <summary>Creates the service.</summary>
    public QrzUploadService(QrzLogbookClient client, QsoRepository qsos, UploadQueueRepository queue, IClock clock,
                            Backoff? backoff = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _client = client;
        _qsos = qsos;
        _queue = queue;
        _clock = clock;
        _backoff = backoff ?? Backoff.Network();
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Current status for the bottom bar.</summary>
    public ServiceStatus Status { get; private set; } = ServiceStatus.Off("Not started");

    /// <summary>Adds a contact to the queue.</summary>
    public void Enqueue(long qsoId) => _queue.Enqueue(qsoId, Target, _clock.UtcNow);

    /// <summary>Uploads every item that is due. Returns the number uploaded (including duplicates).</summary>
    public async Task<int> ProcessDueAsync(string key, CancellationToken ct)
    {
        var done = 0;
        foreach (var item in _queue.Due(Target, _clock.UtcNow))
        {
            ct.ThrowIfCancellationRequested();
            var qso = _qsos.Get(item.QsoId);
            if (qso is null)
            {
                _queue.Remove(item.Id);
                continue;
            }

            var wait = _lastRequestUtc + MinInterval - _clock.UtcNow;
            if (wait > TimeSpan.Zero) await _delay(wait, ct).ConfigureAwait(false);
            _lastRequestUtc = _clock.UtcNow;

            var result = await _client.InsertAsync(key, AdifMapper.ToAdif(qso), ct).ConfigureAwait(false);
            switch (result.Outcome)
            {
                case QrzInsertOutcome.Inserted:
                case QrzInsertOutcome.Duplicate:
                    _qsos.SetUploadState(qso.Id, UploadState.Uploaded, null, result.LogId);
                    _queue.Remove(item.Id);
                    _backoff.Reset();
                    done++;
                    Status = new ServiceStatus(ServiceHealth.Ok, "Uploading to QRZ", _clock.UtcNow);
                    break;
                case QrzInsertOutcome.Rejected:
                    _qsos.SetUploadState(qso.Id, UploadState.Failed, result.Reason);
                    _queue.Remove(item.Id);
                    break;
                case QrzInsertOutcome.AuthFailed:
                    _queue.Retry(item.Id, result.Reason ?? "AUTH", _clock.UtcNow + _backoff.DelayFor(item.Attempts + 1));
                    _qsos.SetUploadState(qso.Id, UploadState.Queued, result.Reason);
                    Status = new ServiceStatus(ServiceHealth.Down, result.Reason ?? "QRZ refused the logbook key", _clock.UtcNow);
                    return done;
                default:
                    _queue.Retry(item.Id, result.Reason ?? "error", _clock.UtcNow + _backoff.DelayFor(item.Attempts + 1));
                    _qsos.SetUploadState(qso.Id, UploadState.Queued, result.Reason);
                    Status = new ServiceStatus(ServiceHealth.Degraded, "Waiting to upload · retrying", _clock.UtcNow);
                    return done;
            }
        }
        return done;
    }
}
