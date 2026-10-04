// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Decoding;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>Runs a session against a manual clock: 10 ms steps, decodes complete before time moves on.</summary>
internal sealed class SimDriver
{
    public static readonly DateTime Start = new(2026, 1, 4, 2, 53, 59, DateTimeKind.Utc);

    public SimDriver(ISlotSource source, SessionConfig config, ISessionLog? log = null)
    {
        Clock = new ManualClock(Start);
        Rig = new SimulatedRig(Clock);
        Output = new NullAudioOutput { Now = () => Clock.UtcNow };
        var freqs = ReferenceData.LoadFrequencies();
        Tx = new Transmitter(Rig, Output, Clock, freqs);
        var jt9 = Jt9Locator.Find() ?? throw new InvalidOperationException("jt9 missing");
        var decoder = new Jt9Decoder(new Jt9Options(jt9, Path.Combine(Path.GetTempPath(), "ft8sim", Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(60)));
        Session = new Session(config, decoder, source, Tx, Clock, ReferenceData.LoadCountries(), freqs, log);
        SlotClock = new SlotClock(Clock, config.Mode);
        SlotClock.Event += Session.OnSlotEvent;
        SlotClock.Poll();
    }

    public ManualClock Clock { get; }

    public SimulatedRig Rig { get; }

    public NullAudioOutput Output { get; }

    public Transmitter Tx { get; }

    public Session Session { get; }

    public SlotClock SlotClock { get; }

    public async Task RunAsync(TimeSpan duration)
    {
        var end = Clock.UtcNow + duration;
        while (Clock.UtcNow < end)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(10));
            SlotClock.Poll();
            Tx.Poll();
            await Session.DecodeIdle;
        }
    }

    public Task RunSlotsAsync(int slots) => RunAsync(TimeSpan.FromSeconds(15 * slots));
}
