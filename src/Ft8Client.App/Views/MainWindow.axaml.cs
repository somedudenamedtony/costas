// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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
    private MiniWindow? _mini;
    private bool _closing;
    private bool _wired;
    private string? _waterfallBand;
    private Audio.Timing.CapturePump? _waterfallPump;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ReceiveWaterfall.ChannelSelected += hz =>
        {
            if (Vm is not { } vm) return;
            vm.ManualOffset = hz.ToString(System.Globalization.CultureInfo.InvariantCulture);
            vm.OffsetManualCommand.Execute(null);
            ReceiveWaterfall.Update(null, hz);
        };
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => Vm?.Activity(), RoutingStrategies.Tunnel);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Avalonia raises Opened again each time a hidden window is shown, which happens on every
        // return from mini mode; wiring twice would open every dialog twice and tick twice.
        if (_wired || Vm is not { } vm) return;
        _wired = true;
        var w = vm.Host.Settings.Current.Window;
        Width = Math.Max(MinWidth, w.Width);
        Height = Math.Max(MinHeight, w.Height);
        if (w.X is { } x && w.Y is { } y) Position = new Avalonia.PixelPoint(x, y);
        vm.Host.Session.Changed += s => Dispatcher.UIThread.Post(() => vm.Apply(s));
        vm.Apply(vm.Host.Session.Snapshot);
        vm.OpenDialog += OpenDialog;
        vm.Mini.ExpandRequested += CloseMini;
        _timer.Tick += (_, _) =>
        {
            vm.Tick();
            var snapshot = vm.Host.Session.Snapshot;
            var pump = vm.Host.Pump;
            if (_waterfallBand != $"{snapshot.Band}/{snapshot.Mode}" || _waterfallPump != pump)
            {
                ReceiveWaterfall.Clear();
                _waterfallBand = $"{snapshot.Band}/{snapshot.Mode}";
                _waterfallPump = pump;
            }
            var row = pump?.Spectrum.TakeLatest();
            ReceiveWaterfall.Update(snapshot.Transmitting ? null : row, snapshot.TxOffsetHz);
        };
        _timer.Start();
        if (!vm.Host.Settings.Current.SetupComplete) Dispatcher.UIThread.Post(() => OpenDialog("setup"));
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _closing = true;
        _timer.Stop();
        _mini?.Close();
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
        if (name == "mini")
        {
            OpenMini(vm);
            return;
        }
        Window w = name switch
        {
            "setup" => new SetupWindow { DataContext = new SetupViewModel(vm.Host) },
            "settings" or "ranking" => new SettingsWindow { DataContext = new SettingsViewModel(vm.Host, name == "ranking") },
            "diagnostics" => new DiagnosticsWindow { DataContext = new DiagnosticsViewModel(vm.Host) },
            "shortcuts" => new ShortcutsWindow(),
            _ => new AboutWindow(vm.Host.Settings, vm.Host.Updates),
        };
        w.ShowDialog(this);
    }

    // Mini mode replaces the main window until the operator expands it or closes the strip.
    private void OpenMini(MainViewModel vm)
    {
        if (_mini is not null) return;
        _mini = new MiniWindow { DataContext = vm.Mini, Escape = vm.Escape };
        if (vm.Host.Settings.Current.Window is { MiniX: { } x, MiniY: { } y }) _mini.Position = new Avalonia.PixelPoint(x, y);
        else _mini.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _mini.Closing += (_, _) => SaveMiniPosition(vm);
        _mini.Closed += (_, _) =>
        {
            _mini = null;
            if (_closing) return;
            Show();
            Activate();
        };
        _mini.Show();
        Hide();
    }

    private void SaveMiniPosition(MainViewModel vm)
    {
        if (_mini is not { } m) return;
        vm.Host.Settings.Update(s =>
        {
            s.Window.MiniX = m.Position.X;
            s.Window.MiniY = m.Position.Y;
        });
    }

    private void CloseMini() => _mini?.Close();

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
            case Key.M when ctrl:
                OpenDialog("mini");
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
