// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Data.Journal;
using Ft8Client.Decoding;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Writes the decode journal and saves slot audio per the Files setting, pruning old WAV files daily. Neither runs in
/// simulation, so replayed recordings do not mix with what was heard on the air.
/// </summary>
public sealed class RecordingService : IDisposable
{
    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly DecodeJournal _journal;
    private readonly WavArchive _wav;
    private readonly IClock _clock;
    private readonly Timer _prune;
    private bool _warned;

    /// <summary>Creates the service.</summary>
    public RecordingService(Session session, AppSettingsAccessor settings, string journalFolder, string wavFolder, IClock clock)
    {
        _session = session;
        _settings = settings;
        _journal = new DecodeJournal(journalFolder);
        _wav = new WavArchive(wavFolder);
        _clock = clock;
        _prune = new Timer(_ => Prune());
    }

    /// <summary>Starts recording.</summary>
    public void Start()
    {
        _session.SlotDecoded += OnSlotDecoded;
        _session.SlotAudioDecoded += OnSlotAudio;
        _session.Transmitting += OnTransmitting;
        _prune.Change(TimeSpan.FromMinutes(1), TimeSpan.FromHours(24));
    }

    private void OnSlotDecoded(DateTime slot, Mode mode, IReadOnlyList<Decode> decodes)
    {
        var dial = _session.Snapshot.DialHz;
        var name = ModeInfo.Name(mode);
        Guard(() => _journal.Append(slot, decodes.Select(d => DecodeJournal.Format(slot, dial, false, name, d.Snr, d.Dt, d.OffsetHz, d.Text))));
    }

    private void OnTransmitting(PreparedTx tx, DateTime startedUtc)
    {
        var slot = SlotMath.SlotStart(startedUtc, tx.Mode);
        var line = DecodeJournal.Format(slot, _session.Snapshot.DialHz, true, ModeInfo.Name(tx.Mode), 0, 0, tx.OffsetHz, tx.Plan.Message);
        Guard(() => _journal.Append(slot, [line]));
    }

    private void OnSlotAudio(SlotAudio audio, int decodes)
    {
        if (WavArchive.ShouldSave(_settings.Current.Files.SaveWav, decodes)) Guard(() => _wav.Save(audio));
    }

    private void Prune()
    {
        Guard(() =>
        {
            var n = _wav.Prune(_clock.UtcNow, _settings.Current.Files.RetentionDays);
            if (n > 0) Log.Information("Deleted {Count} saved WAV files past retention", n);
        });
    }

    private void Guard(Action a)
    {
        try
        {
            a();
            _warned = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_warned) return;
            _warned = true;
            Log.Warning(ex, "Could not write the journal or a WAV file");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session.SlotDecoded -= OnSlotDecoded;
        _session.SlotAudioDecoded -= OnSlotAudio;
        _session.Transmitting -= OnTransmitting;
        _prune.Dispose();
    }
}
