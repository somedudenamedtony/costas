// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Audio.Backends;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Reference;
using Ft8Client.Data.Settings;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public sealed class TxLevelControllerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("costas-txlevel-").FullName;
    private readonly AppSettingsAccessor _settings;
    private readonly Transmitter _tx;
    private readonly TxLevelController _level;

    public TxLevelControllerTests()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var s = new AppSettings();
        s.Operating.Band = "20m";
        s.Profile.Audio.TxGainDb = -10;
        _settings = new AppSettingsAccessor(store, s);
        var clock = new ManualClock(new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc));
        _tx = new Transmitter(new SimulatedRig(clock), new NullAudioOutput(), clock, ReferenceData.LoadFrequencies());
        _level = new TxLevelController(_tx, _settings);
        _settings.Changed += _ => _level.Apply();
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static TxMeterResult Meters(double alc) => TxMeterCheck.Evaluate([new TxMeterReading(alc, 1.2)]);

    [Fact]
    public void Constructor_NoBandLevel_UsesSettingsLevel()
    {
        _tx.GainDb.Should().Be(-10);
    }

    [Fact]
    public void OnMeasured_HighAlc_LowersAndRemembersForTheBandAndSaysSo()
    {
        var shown = _level.OnMeasured(Meters(0.6));

        _tx.GainDb.Should().Be(-13);
        _settings.Current.Profile.Audio.TxGainByBand.Should().Equal(new Dictionary<string, double> { ["20m"] = -13 });
        shown.Warning.Should().EndWith("Costas lowered its audio level to -13.0 dB for the next transmission.");
        shown.Warning.Should().NotContain(TxMeterCheck.AlcAdvice);
    }

    [Fact]
    public void Apply_BandChange_UsesThatBandsOwnLevel()
    {
        _level.OnMeasured(Meters(0.6));
        _settings.Update(s => s.Operating.Band = "40m");
        _tx.GainDb.Should().Be(-10);
        _settings.Update(s => s.Operating.Band = "20m");
        _tx.GainDb.Should().Be(-13);
    }

    [Fact]
    public void OnMeasured_NoAlc_RisesNoMoreThanSixDecibelsAboveSettingsLevel()
    {
        for (var i = 0; i < 20; i++) _level.OnMeasured(Meters(0.0));
        _tx.GainDb.Should().Be(-10 + TxLevelController.AutoHeadroomDb);
    }

    [Fact]
    public void OnMeasured_AutomaticOff_LeavesLevelAndAdvice()
    {
        _settings.Update(s => s.Profile.Audio.AutoTxLevel = false);
        var r = Meters(0.9);
        _level.OnMeasured(r).Should().Be(r);
        _tx.GainDb.Should().Be(-10);
        _settings.Current.Profile.Audio.TxGainByBand.Should().BeEmpty();
    }

    [Fact]
    public void OnMeasured_RadioReportsNoAlc_LeavesLevel()
    {
        var r = TxMeterCheck.Evaluate([new TxMeterReading(null, 1.5)]);
        _level.OnMeasured(r).Should().Be(r);
        _tx.GainDb.Should().Be(-10);
    }
}
