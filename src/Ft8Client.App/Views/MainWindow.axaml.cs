// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
            // In a keyboard mode a click also reads the signal there, and keeps reading it while it is quiet.
            if (!Core.ModeInfo.IsSlotted(vm.Host.Session.Config.Mode)) vm.Host.Keyboard.Listen(hz);
        };
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnText, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _held.Reset();
        AddHandler(PointerPressedEvent, (_, _) => Vm?.Activity(), RoutingStrategies.Tunnel);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // A held function key repeats: its macro goes once per press.
    private readonly KeyRepeatFilter _held = new();

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
        vm.AttentionRequested += () => TaskbarFlash.Flash(_mini is { } m ? m : this);
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
            ReceiveWaterfall.SetTags(Core.ModeInfo.IsSlotted(snapshot.Mode) ? [] : Tags(snapshot));
        };
        _timer.Start();
        if (!vm.Host.Settings.Current.SetupComplete) Dispatcher.UIThread.Post(() => OpenDialog("setup"));
    }

    // Keyboard modes: a tag over each station being read, "?" over a signal with no callsign yet, the one in contact red.
    private static List<(int Hz, string Label, bool InContact)> Tags(Engine.SessionSnapshot s)
    {
        var partner = s.Contact is { Outcome: Core.Contacts.ContactOutcome.InProgress } c ? c.DxCall : null;
        var tags = s.Stations.Where(st => st.Parity is null && st.HeardInLastOwnSlot)
            .Select(st => (st.OffsetHz, st.Call, partner is not null && Core.Messages.Callsign.EqualsCall(st.Call, partner))).ToList();
        tags.AddRange(s.UnnamedSignals.Where(hz => tags.All(t => Math.Abs(t.Item1 - hz) > 20)).Select(hz => (hz, "?", false)));
        return tags;
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

    // True for a key aimed at an open dropdown, menu or flyout, which handles it itself (Esc closes it).
    private bool InPopup(object? source) => source switch
    {
        ComboBox { IsDropDownOpen: true } or MenuItem => true,
        Visual v => TopLevel.GetTopLevel(v) != this || v.FindAncestorOfType<OverlayPopupHost>(includeSelf: true) is not null,
        _ => false,
    };

    // PSK and RTTY on Operate: typing belongs to the type-ahead line wherever the keyboard is.
    private bool KeyboardTyping(MainViewModel vm, object? source) =>
        vm.Tab == 0 && vm.Operate.Keyboard.Visible && source is not TextBox && !InPopup(source);

    private void OnText(object? sender, TextInputEventArgs e)
    {
        if (Vm is not { } vm || string.IsNullOrEmpty(e.Text) || !KeyboardTyping(vm, e.Source)) return;
        if (OperatePage.TakeText(e.Text)) e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _held.Release(e.Key);
        if (Vm is { } vm && vm.Operate.Keyboard.Visible) OperatePage.TakeKeyUp(e);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var fresh = _held.Press(e.Key);
        if (Vm is not { } vm) return;
        vm.Activity();
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var typing = e.Source is TextBox;
        switch (e.Key)
        {
            // Esc aimed at an open dropdown or menu closes it, and halts only when something is on the air.
            case Key.Escape when InPopup(e.Source):
                if (vm.Host.Session.Snapshot.Transmitting) vm.HaltCommand.Execute(null);
                break;
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
            case Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5 when ctrl:
                vm.Tab = e.Key - Key.D1;
                e.Handled = true;
                break;
            case Key.M when ctrl:
                OpenDialog("mini");
                e.Handled = true;
                break;
            // PSK and RTTY: the function keys send their macros.
            case >= Key.F1 and <= Key.F12 when vm.Operate.Keyboard.Visible:
                if (fresh) vm.Operate.Keyboard.SendMacro(e.Key - Key.F1 + 1);
                e.Handled = true;
                break;
            case Key.F1:
                OpenDialog("shortcuts");
                e.Handled = true;
                break;
            // PSK and RTTY: Enter does what it does in the type-ahead line (sends what is typed, or the next step's
            // macro, which the hint names) wherever the keyboard is.
            case Key.Enter when KeyboardTyping(vm, e.Source):
                vm.Operate.Keyboard.Enter();
                e.Handled = true;
                break;
            case Key.Enter when vm.Tab == 0 && !typing && !vm.Operate.Keyboard.Visible:
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
            // PSK and RTTY: Space and Backspace go to the type-ahead line, not to the button that has the keyboard.
            case Key.Space or Key.Back when !ctrl && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && KeyboardTyping(vm, e.Source):
                e.Handled = OperatePage.TakeKey(e);
                break;
        }
    }
}
