// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Integrations.Updates;

namespace Ft8Client.App.ViewModels;

/// <summary>The bar that offers a newer build: install (download, verify, run the installer and close), release notes, or skip.</summary>
public sealed partial class UpdateBarViewModel : ObservableObject
{
    private readonly UpdateService _updates;
    private readonly Func<bool> _busyOnAir;
    private readonly Action _shutdown;

    /// <summary>Creates the bar. <paramref name="busyOnAir"/> is true while transmitting or in a contact.</summary>
    public UpdateBarViewModel(UpdateService updates, Func<bool> busyOnAir, Action shutdown)
    {
        _updates = updates;
        _busyOnAir = busyOnAir;
        _shutdown = shutdown;
        updates.Changed += () => MainViewModel.Dispatch(Refresh);
        Refresh();
    }

    /// <summary>Shown when a newer build is available.</summary>
    [ObservableProperty]
    public partial bool Visible { get; set; }

    /// <summary>"Costas 0.1.0.42 is available (you have 0.1.0.40)."</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Downloading or about to start the installer.</summary>
    [ObservableProperty]
    public partial bool Busy { get; set; }

    /// <summary>Progress or a problem.</summary>
    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    private void Refresh()
    {
        var r = _updates.Available;
        Visible = r is not null;
        if (r is not null) Text = $"{AppInfo.ProductName} {r.Version} is available (you have {AppInfo.Version}).";
    }

    /// <summary>Downloads and verifies the installer, starts it, and closes the app so it can be upgraded.</summary>
    [RelayCommand]
    private async Task Install()
    {
        if (Busy) return;
        if (_busyOnAir())
        {
            Detail = "Finish the contact (or halt) first; the app closes to install.";
            return;
        }
        Busy = true;
        try
        {
            var progress = new Progress<double>(p => Detail = $"Downloading… {p:P0}");
            var path = await _updates.DownloadAsync(progress, CancellationToken.None);
            if (_busyOnAir())
            {
                Detail = "Downloaded. Finish the contact, then press Install again.";
                return;
            }
            Detail = "Starting the installer…";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            _shutdown();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException
                                       or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Detail = $"Update failed: {ex.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Opens the release page.</summary>
    [RelayCommand]
    private void ReleaseNotes()
    {
        if (_updates.Available is { PageUrl: { Length: > 0 } url } && url.StartsWith("https://", StringComparison.Ordinal))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>Does not offer this build again.</summary>
    [RelayCommand]
    private void Skip() => _updates.Skip();
}
