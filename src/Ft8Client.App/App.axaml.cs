// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Serilog;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;

namespace Ft8Client.App;

/// <summary>The Avalonia application.</summary>
public sealed class App : Application
{
    private AppearanceService? _appearance;

    /// <summary>The host, set by Program before start.</summary>
    public static AppHost? Host { get; set; }

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _appearance = new AppearanceService(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Host is { } host)
        {
            ApplyTheme(host);
            // A theme chosen in Settings takes effect at once, not at the next start.
            host.Settings.Changed += _ => Dispatcher.UIThread.Post(() => ApplyTheme(host));
            MainViewModel.Dispatch = a => Dispatcher.UIThread.Post(a);
            MainViewModel.Shutdown = () => Dispatcher.UIThread.Post(() => desktop.Shutdown());
            var vm = new MainViewModel(host);
            desktop.MainWindow = new MainWindow { DataContext = vm };
            WatchForUnexpectedErrors(host, vm);
            // Exit, not ShutdownRequested: a shutdown for an update is forced and never raises ShutdownRequested, which
            // skipped the final saves (neighbour history, logs) and the services' clean stop.
            desktop.Exit += (_, _) => host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(AppHost host)
    {
        _appearance?.Apply(host.Settings.Current.Appearance);
    }

    // An error nothing caught (a command failing on a driver or system error) used to close the app without a word.
    // Now transmit is stopped, the error is logged and shown on the status line, and the app carries on, unless errors
    // keep coming, which ends it as before (Program.Main still releases PTT on the way out).
    private static void WatchForUnexpectedErrors(AppHost host, MainViewModel vm)
    {
        var policy = new UnexpectedErrorPolicy();
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unexpected error on the UI thread");
            _ = HaltAsync(host);
            if (!policy.Survive(DateTime.UtcNow)) return;
            e.Handled = true;
            vm.StatusText = UnexpectedErrorPolicy.Describe(e.Exception);
            vm.StatusKind = TextKind.Critical;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task error");
            e.SetObserved();
        };
    }

    private static async Task HaltAsync(AppHost host)
    {
        try
        {
            await host.Session.HaltAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PTT release after an unexpected error failed");
        }
    }
}
