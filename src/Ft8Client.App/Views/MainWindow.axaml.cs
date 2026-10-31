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
using Ft8Client.App.Services;
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
    private long _waterfallSlot = -1;
    private WaterfallWindow? _waterfallWindow;
    private const double CompactWaterfall = 52;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ReceiveWaterfall.ChannelSelected += SelectChannel;
        WaterfallGrip.DragDelta += (_, e) => SetWaterfallHeight(WaterfallHeight - e.Vector.Y, save: false);
        WaterfallGrip.DragCompleted += (_, _) => SetWaterfallHeight(WaterfallHeight, save: true);
        WaterfallGrip.DoubleTapped += (_, e) =>
        {
            SetWaterfallHeight(CompactWaterfall, save: true);
            e.Handled = true;
        };
        WaterfallGrip.KeyDown += OnGripKey;
        PopOutButton.Click += (_, _) => PopOutWaterfall();
        AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Button button && button.Classes.Contains("instrument-dial"))
            {
                DialFrequencyButton.Flyout?.ShowAt(button);
                e.Handled = true;
            }
        });
        SizeChanged += (_, _) => SetWaterfallHeight(WaterfallHeight, save: false);
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
        SetWaterfallHeight(w.WaterfallHeight, save: false);
        OperatePage.LoadColumns(w.ColumnWidths);
        OperatePage.ColumnsSaved += (name, widths) => vm.Host.Settings.Update(s => s.Window.ColumnWidths[name] = [.. widths]);
        if (w.WaterfallOut) Dispatcher.UIThread.Post(PopOutWaterfall);
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
                _waterfallWindow?.View.Clear();
                _waterfallBand = $"{snapshot.Band}/{snapshot.Mode}";
                _waterfallPump = pump;
                if (_waterfallWindow is { } ww) ww.Title = $"Costas waterfall · {snapshot.Band} {snapshot.Mode.ToString().ToUpperInvariant()}";
            }
            var row = snapshot.Transmitting ? null : pump?.Spectrum.TakeLatest();
            // Marks the first row of each receive slot, so a tall waterfall can draw the slot boundaries. PSK and RTTY
            // have no slots.
            var slotted = Core.ModeInfo.IsSlotted(snapshot.Mode);
            var slotStart = false;
            if (slotted)
            {
                var slot = vm.Host.Clock.UtcNow.Ticks / TimeSpan.TicksPerMillisecond / Core.ModeInfo.SlotMilliseconds(snapshot.Mode);
                slotStart = row is not null && slot != _waterfallSlot;
                if (row is not null) _waterfallSlot = slot;
            }
            // Both views are fed so the strip and the window each have full history when switching between them.
            ReceiveWaterfall.Update(row, snapshot.TxOffsetHz, slotStart);
            _waterfallWindow?.View.Update(row, snapshot.TxOffsetHz, slotStart);
            var tags = slotted ? [] : Tags(snapshot);
            ReceiveWaterfall.SetTags(tags);
            _waterfallWindow?.View.SetTags(tags);
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
            if (_waterfallWindow is { } ww)
            {
                SaveWaterfallWindow(vm, ww, stillOut: true);
                ww.Close();
            }
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

    private void SelectChannel(int hz)
    {
        if (Vm is not { } vm) return;
        vm.ManualOffset = hz.ToString(System.Globalization.CultureInfo.InvariantCulture);
        vm.OffsetManualCommand.Execute(null);
        ReceiveWaterfall.Update(null, hz);
        _waterfallWindow?.View.Update(null, hz);
        // In a keyboard mode a click also reads the signal there, and keeps reading it while it is quiet.
        if (!Core.ModeInfo.IsSlotted(vm.Host.Session.Config.Mode)) vm.Host.Keyboard.Listen(hz);
    }

    private double WaterfallHeight => BottomGrid.RowDefinitions[2].Height.Value;

    // The strip grows up to half the window, so the panels above it always keep room; the panels scroll when short.
    private void SetWaterfallHeight(double height, bool save)
    {
        var max = Math.Max(CompactWaterfall, Math.Round(Bounds.Height / 2));
        height = Math.Round(Math.Clamp(height, CompactWaterfall, max));
        if (_waterfallWindow is null && height != WaterfallHeight) BottomGrid.RowDefinitions[2].Height = new GridLength(height);
        if (!save || Vm is not { } vm) return;
        vm.Host.Settings.Update(s => s.Window.WaterfallHeight = height);
    }

    private void OnGripKey(object? sender, KeyEventArgs e)
    {
        var height = e.Key switch
        {
            Key.Up => WaterfallHeight + 20,
            Key.Down => WaterfallHeight - 20,
            Key.Home => CompactWaterfall,
            _ => (double?)null,
        };
        if (height is not { } h) return;
        SetWaterfallHeight(h, save: true);
        e.Handled = true;
    }

    // The waterfall is shown in one place at a time: popping it out hides the strip and its grip, popping it in brings them back.
    private void PopOutWaterfall()
    {
        if (_waterfallWindow is not null || Vm is not { } vm) return;
        var w = vm.Host.Settings.Current.Window;
        var window = new WaterfallWindow
        {
            Escape = vm.Escape,
            Width = Math.Max(420, w.WaterfallWindowWidth),
            Height = Math.Max(120, w.WaterfallWindowHeight),
            Title = $"Costas waterfall · {vm.Host.Session.Snapshot.Band} {vm.Host.Session.Snapshot.Mode.ToString().ToUpperInvariant()}",
        };
        if (w is { WaterfallX: { } x, WaterfallY: { } y } && OnSomeScreen(new PixelPoint(x, y))) window.Position = new PixelPoint(x, y);
        else window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.View.CopyFrom(ReceiveWaterfall);
        window.View.ChannelSelected += SelectChannel;
        window.Closing += (_, _) =>
        {
            // Closing the main window closes this one too, and it should open again next time.
            if (!_closing) SaveWaterfallWindow(vm, window, stillOut: false);
        };
        window.Closed += (_, _) =>
        {
            _waterfallWindow = null;
            if (_closing) return;
            ReceiveWaterfall.CopyFrom(window.View);
            ShowStrip(true);
        };
        _waterfallWindow = window;
        ShowStrip(false);
        window.Show(this);
        vm.Host.Settings.Update(s => s.Window.WaterfallOut = true);
    }

    private void ShowStrip(bool show)
    {
        WaterfallStrip.IsVisible = show;
        WaterfallGrip.IsVisible = show;
        BottomGrid.RowDefinitions[2].Height = new GridLength(show ? Math.Max(CompactWaterfall, Vm?.Host.Settings.Current.Window.WaterfallHeight ?? CompactWaterfall) : 0);
        if (show) SetWaterfallHeight(WaterfallHeight, save: false);
    }

    private static void SaveWaterfallWindow(MainViewModel vm, WaterfallWindow window, bool stillOut) =>
        vm.Host.Settings.Update(s =>
        {
            s.Window.WaterfallOut = stillOut;
            s.Window.WaterfallX = window.Position.X;
            s.Window.WaterfallY = window.Position.Y;
            s.Window.WaterfallWindowWidth = window.Width;
            s.Window.WaterfallWindowHeight = window.Height;
        });

    // A window remembered on a monitor that is no longer connected opens centred over Costas instead.
    private bool OnSomeScreen(PixelPoint point) => Screens.All.Any(s => s.WorkingArea.Contains(point + new PixelPoint(40, 20)));

    private void OpenDialog(string name)
    {
        if (Vm is not { } vm) return;
        vm.TrackWindow(name);
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
            "feedback" => NewFeedbackWindow(vm.Host),
            _ => new AboutWindow(vm.Host.Settings, vm.Host.Updates),
        };
        w.ShowDialog(this);
    }

    // The feedback HTTP client leaves the callsign out of its User-Agent: a report identifies the station only if the
    // operator types their callsign or ticks the log.
    private static FeedbackWindow NewFeedbackWindow(AppHost host)
    {
        var http = AppHost.FeedbackEndpoint is null ? null : Ft8Client.Integrations.Net.HttpFactory.Create(string.Empty, timeout: TimeSpan.FromSeconds(20));
        var client = http is null ? null : new Ft8Client.Integrations.Feedback.FeedbackClient(http, AppHost.FeedbackEndpoint!);
        var secrets = Ft8Client.Data.Secrets.SecretNames.All(host.Settings.Current.Profiles.Select(p => p.Id))
            .Select(host.Secrets.Get).Where(v => v is not null).Select(v => v!).ToList();
        var w = new FeedbackWindow
        {
            DataContext = new FeedbackViewModel(client, AppHost.FeedbackEmail, () => LogTail.Read(host.Paths.Logs, secrets), Browser.OpenMail),
        };
        w.Closed += (_, _) => http?.Dispose();
        return w;
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
            _waterfallWindow?.Show(this);
            Activate();
        };
        _mini.Show();
        _waterfallWindow?.Hide();
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
        // A drop-down, menu or flyout is open: its keys are its own (Esc closes it; it must not abandon the contact).
        var inPopup = InPopup(e.Source);
        // Enter and the arrows belong to the control that has focus (a button, a drop-down); only on the station list or
        // nothing in particular do they move through Next in line and call. Enter on Halt Tx must never call a station.
        var listKeys = vm.Tab == 0 && !typing && !inPopup
                       && e.Source is not (Button or ComboBox or ComboBoxItem or MenuItem or Slider or NumericUpDown);
        // Ctrl+1 to Ctrl+5 and Ctrl+Tab switch views, even from a text box or the typing line (they type nothing). An open
        // drop-down or menu keeps its keys.
        if (!inPopup && TabKeys.Target(e.Key, e.KeyModifiers, vm.Tab) is { } tab)
        {
            vm.Tab = tab;
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            // Esc aimed at an open dropdown or menu closes it, and halts only when something is on the air.
            case Key.Escape when inPopup:
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
            case Key.Enter when listKeys && !vm.Operate.Keyboard.Visible:
                vm.Operate.CallSelected();
                e.Handled = true;
                break;
            case Key.Up when listKeys:
                vm.Operate.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down when listKeys:
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
