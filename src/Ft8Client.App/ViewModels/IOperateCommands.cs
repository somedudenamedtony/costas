// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>Commands the Operate view sends to the session.</summary>
public interface IOperateCommands
{
    /// <summary>Call a station.</summary>
    void Call(string call);

    /// <summary>Answer a waiting caller now.</summary>
    void Answer(string call);

    /// <summary>Answer a waiting caller after this contact.</summary>
    void AnswerAfter(string call);

    /// <summary>Resend the current step.</summary>
    void Resend();

    /// <summary>Log now.</summary>
    void LogNow();

    /// <summary>Abandon the contact.</summary>
    void Abandon();

    /// <summary>Jump to a step.</summary>
    void JumpTo(int step);

    /// <summary>Look up a station (QRZ) for the details flyout.</summary>
    void Lookup(string call);

    /// <summary>Open the reach view.</summary>
    void OpenReach();

    /// <summary>Open the log.</summary>
    void OpenLog();

    /// <summary>Open the ranking settings.</summary>
    void OpenRankingSettings();

    /// <summary>Retry a failed upload.</summary>
    void RetryUpload(long qsoId);
}
