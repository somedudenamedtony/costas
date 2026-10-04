// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>The main window: wires snapshots, timers and keyboard shortcuts to the view model.</summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => Vm?.Activity(), RoutingStrategies.Tunnel);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Vm is not { } vm) return;
        var w = vm.Host.Settings.Current.Window;
        Width = Math.Max(MinWidth, w.Width);
        Height = Math.Max(MinHeight, w.Height);
        if (w.X is { } x && w.Y is { } y) Position = new Avalonia.PixelPoint(x, y);
        vm.Host.Session.Changed += s => Dispatcher.UIThread.Post(() => vm.Apply(s));
        vm.Apply(vm.Host.Session.Snapshot);
        vm.OpenDialog += OpenDialog;
        _timer.Tick += (_, _) => vm.Tick();
        _timer.Start();
        if (!vm.Host.Settings.Current.SetupComplete) Dispatcher.UIThread.Post(() => OpenDialog("setup"));
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (Vm is { } vm)
        {
            var size = ClientSize;
            vm.Host.Settings.Update(s =>
            {
                s.Window.Width = size.Width;
                s.Window.Height = size.Height;
                s.Window.X = Position.X;
                s.Window.Y = Position.Y;
            });
        }
        base.OnClosing(e);
    }

    private void OpenDialog(string name)
    {
        if (Vm is not { } vm) return;
        Window w = name switch
        {
            "setup" => new SetupWindow { DataContext = new SetupViewModel(vm.Host) },
            "settings" or "ranking" => new SettingsWindow { DataContext = new SettingsViewModel(vm.Host, name == "ranking") },
            "diagnostics" => new DiagnosticsWindow { DataContext = new DiagnosticsViewModel(vm.Host) },
            "shortcuts" => new ShortcutsWindow(),
            _ => new AboutWindow(vm.Host.Settings),
        };
        w.ShowDialog(this);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.Activity();
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var typing = e.Source is TextBox;
        switch (e.Key)
        {
            case Key.Escape:
                vm.Escape();
                e.Handled = true;
                break;
            case Key.Q when ctrl:
                vm.ToggleCqCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R when ctrl:
                vm.Resend();
                e.Handled = true;
                break;
            case Key.L when ctrl:
                vm.LogNow();
                e.Handled = true;
                break;
            case Key.D1 or Key.D2 or Key.D3 or Key.D4 when ctrl:
                vm.Tab = e.Key - Key.D1;
                e.Handled = true;
                break;
            case Key.F1:
                OpenDialog("shortcuts");
                e.Handled = true;
                break;
            case Key.Enter when vm.Tab == 0 && !typing:
                vm.Operate.CallSelected();
                e.Handled = true;
                break;
            case Key.Up when vm.Tab == 0 && !typing:
                vm.Operate.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down when vm.Tab == 0 && !typing:
                vm.Operate.MoveSelection(1);
                e.Handled = true;
                break;
        }
    }
}
