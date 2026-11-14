// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.UpdateSplash.Tests;

public sealed class SplashProgressTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Update_NormalUpdate_GoesClosingInstallingOpeningDone()
    {
        var p = new SplashProgress();

        p.Update(S(0), oldExited: false, started: false, ready: false).Should().Be(SplashPhase.Closing);
        p.Status("0.1.312").Should().Be("Closing Costas…");
        p.Update(S(2), oldExited: true, started: false, ready: false).Should().Be(SplashPhase.Installing);
        p.Status("0.1.312").Should().Be("Installing version 0.1.312…");
        p.Update(S(4), oldExited: true, started: true, ready: false).Should().Be(SplashPhase.Opening);
        p.Status("0.1.312").Should().Be("Opening Costas 0.1.312…");
        p.Update(S(5), oldExited: true, started: true, ready: true).Should().Be(SplashPhase.Done);
    }

    [Fact]
    public void Update_ReadyWithoutTheOtherSignals_IsDone()
    {
        // A Costas that started before the splash looked (fast machine) still closes it.
        new SplashProgress().Update(S(1), oldExited: false, started: false, ready: true).Should().Be(SplashPhase.Done);
    }

    [Fact]
    public void Update_StartedSeenBeforeExit_SkipsToOpeningAndNeverGoesBack()
    {
        var p = new SplashProgress();
        p.Update(S(1), oldExited: false, started: true, ready: false).Should().Be(SplashPhase.Opening);
        p.Update(S(2), oldExited: true, started: true, ready: false).Should().Be(SplashPhase.Opening);
    }

    [Fact]
    public void Update_NothingRestartsAfterInstalling_GivesUpAfterTheLimit()
    {
        var p = new SplashProgress();
        p.Update(S(3), oldExited: true, started: false, ready: false);

        p.Update(S(3) + SplashProgress.InstallingLimit - S(1), true, false, false).Should().Be(SplashPhase.Installing);
        p.Update(S(3) + SplashProgress.InstallingLimit, true, false, false).Should().Be(SplashPhase.Done);
    }

    [Fact]
    public void Update_OldCostasNeverExits_GivesUpAfterTheLimit()
    {
        var p = new SplashProgress();
        p.Update(SplashProgress.ClosingLimit, false, false, false).Should().Be(SplashPhase.Done);
    }

    [Fact]
    public void Update_NewCostasNeverShowsItsWindow_GivesUpAfterTheLimit()
    {
        var p = new SplashProgress();
        p.Update(S(5), true, true, false);
        p.Update(S(5) + SplashProgress.OpeningLimit, true, true, false).Should().Be(SplashPhase.Done);
    }

    [Fact]
    public void Status_SlowInstall_SaysItIsStillGoing()
    {
        var p = new SplashProgress();
        p.Update(S(1), true, false, false);
        p.Update(S(1) + SplashProgress.SlowInstall, true, false, false);

        p.Status("0.1.312").Should().Be("Still installing version 0.1.312; this can take a minute…");
        p.Status(string.Empty).Should().Be("Still installing; this can take a minute…");
    }

    [Fact]
    public void Status_NoVersion_LeavesItOut()
    {
        var p = new SplashProgress();
        p.Update(S(1), true, false, false);
        p.Status(string.Empty).Should().Be("Installing the update…");
        p.Update(S(2), true, true, false);
        p.Status(string.Empty).Should().Be("Opening Costas…");
    }
}
