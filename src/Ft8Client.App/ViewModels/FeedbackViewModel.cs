// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.Core;
using Ft8Client.Integrations.Feedback;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// Report a problem: the operator writes what happened and chooses what goes with it. Version and operating system
/// are ticked by default (nothing personal); the log, which can hold callsigns and frequencies, is off until ticked.
/// Nothing is sent until Send is pressed.
/// </summary>
public sealed partial class FeedbackViewModel : ObservableObject
{
    private readonly FeedbackClient? _client;
    private readonly string? _email;
    private readonly Func<string?> _readLog;
    private readonly Func<string, bool> _openMail;

    /// <summary>Creates the view model.</summary>
    /// <param name="client">The form relay, or null when this build has none.</param>
    /// <param name="email">The address for the email button, or null when this build has none.</param>
    /// <param name="readLog">Reads the end of the app log (secrets already removed), or null when there is none.</param>
    /// <param name="openMail">Opens a mailto link; false when nothing could.</param>
    public FeedbackViewModel(FeedbackClient? client, string? email, Func<string?> readLog, Func<string, bool> openMail)
    {
        _client = client;
        _email = email;
        _readLog = readLog;
        _openMail = openMail;
    }

    /// <summary>What happened, or the idea.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(EmailCommand))]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>An email address for a reply (optional).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(EmailCommand))]
    public partial string ReplyTo { get; set; } = string.Empty;

    /// <summary>Adds the app version and operating system.</summary>
    [ObservableProperty]
    public partial bool IncludeSystem { get; set; } = true;

    /// <summary>Adds the end of the app log. Off by default: it can hold callsigns and frequencies.</summary>
    [ObservableProperty]
    public partial bool IncludeLog { get; set; }

    /// <summary>The result line under the buttons.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>True while a report is on its way.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool Sending { get; set; }

    /// <summary>True once a report has gone; the window then offers Close.</summary>
    [ObservableProperty]
    public partial bool Sent { get; set; }

    /// <summary>True when this build can send directly.</summary>
    public bool CanSendDirect => _client is not null;

    /// <summary>True when this build has an address for the email button.</summary>
    public bool HasEmail => _email is not null;

    /// <summary>Shown when the build has neither route (a developer build).</summary>
    public bool NoRoute => _client is null && _email is null;

    /// <summary>The version and operating system line, as it will be sent.</summary>
    public static string SystemLine =>
        $"{AppInfo.ProductName} {AppInfo.Version} · {RuntimeInformation.OSDescription.Trim()} · {RuntimeInformation.OSArchitecture} · .NET {Environment.Version}";

    /// <summary>The reply address problem, or empty.</summary>
    public string ReplyToError => FeedbackReport.IsReplyAddressValid(ReplyTo) ? string.Empty : "That does not look like an email address.";

    partial void OnReplyToChanged(string value) => OnPropertyChanged(nameof(ReplyToError));

    /// <summary>The report as it stands, with only what the operator ticked.</summary>
    public FeedbackReport Build() => new(
        Message.Trim(),
        string.IsNullOrWhiteSpace(ReplyTo) ? null : ReplyTo.Trim(),
        IncludeSystem ? SystemLine : null,
        IncludeLog ? _readLog() : null);

    private bool Ready => Message.Trim().Length > 0 && Message.Length <= FeedbackReport.MaxMessageLength && FeedbackReport.IsReplyAddressValid(ReplyTo);

    private bool CanSend() => _client is not null && Ready && !Sending;

    /// <summary>Sends the report through the form relay.</summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_client is null) return;
        Sending = true;
        Status = "Sending…";
        try
        {
            var error = await _client.SendAsync(Build(), CancellationToken.None);
            Sent = error is null;
            Status = error ?? (string.IsNullOrWhiteSpace(ReplyTo) ? "Sent. Thank you." : "Sent. Thank you: any reply will go to the address you gave.");
        }
        finally
        {
            Sending = false;
        }
    }

    private bool CanEmail() => _email is not null && Ready;

    /// <summary>Opens the operator's mail program with the report filled in (without the log, which is too long for a link).</summary>
    [RelayCommand(CanExecute = nameof(CanEmail))]
    private void Email()
    {
        if (_email is null) return;
        Status = _openMail(FeedbackMail.Link(_email, Build()))
            ? IncludeLog ? "Your mail program has the report. The log does not fit in an email link: use Copy and paste it in." : "Your mail program has the report: send it from there."
            : $"No mail program opened. Use Copy and send it to {_email}.";
    }

    /// <summary>The full text for Copy, with everything ticked.</summary>
    public string CopyText() => Build().Text();
}
