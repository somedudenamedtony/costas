// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Keyboard;

namespace Ft8Client.App.Views;

/// <summary>The Operate view.</summary>
public partial class OperateView : UserControl
{
    /// <summary>"Show all" while Watching is folded, "Fold" once opened.</summary>
    public static readonly IValueConverter WatchingToggleText = new FuncValueConverter<bool, string>(open => open ? "Fold" : "Show all");

    /// <summary>Creates the view.</summary>
    public OperateView()
    {
        InitializeComponent();
        LineList.PointerEntered += (_, _) => Vm?.PointerEnteredLine();
        LineList.PointerExited += (_, _) => Vm?.PointerExitedLine();
        LineList.AddHandler(PointerPressedEvent, OnRowPressed, RoutingStrategies.Tunnel);
        RepeatList.PointerEntered += (_, _) => Vm?.PointerEnteredRepeats();
        RepeatList.PointerExited += (_, _) => Vm?.PointerExitedRepeats();
        LineList.DoubleTapped += (_, _) =>
        {
            if (Vm?.Selected is { } row) Vm.CallSelected();
        };
        SizeChanged += (_, e) => Relayout(e.NewSize.Width);
        TypeAhead.AddHandler(KeyDownEvent, OnTypeAheadKey, RoutingStrategies.Tunnel);
        TypeAhead.AddHandler(TextInputEvent, OnTypeAheadText, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (Vm is not { } vm) return;
            vm.Keyboard.Conversation.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(() => ConversationScroll.ScrollToEnd());
            vm.Keyboard.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(KeyboardPanelViewModel.BlockedMacro)) ShowBlocked(vm.Keyboard.BlockedMacro);
            };
        };
    }

    // PSK and RTTY: Enter sends; on the air, Backspace takes back what has not gone yet.
    private void OnTypeAheadKey(object? sender, KeyEventArgs e)
    {
        if (Vm?.Keyboard is not { } k) return;
        if (e.Key == Key.Enter)
        {
            k.Enter();
            e.Handled = true;
        }
        else if (e.Key == Key.Back && k.OnAir)
        {
            k.Backspace();
            e.Handled = true;
        }
    }

    // The refused macro as it would go out, each field with no value in red (docs/09-keyboard-modes.md section 8).
    private void ShowBlocked(IReadOnlyList<MacroSegment> segments)
    {
        var inlines = BlockedLine.Inlines ??= [];
        inlines.Clear();
        foreach (var s in segments)
        {
            var run = new Run(s.Text);
            if (s.Missing)
            {
                // FindResource does not see the theme dictionaries, so it returned null and the field showed blank.
                run.Foreground = Converters.Brush("CriticalBrush");
                run.FontWeight = FontWeight.SemiBold;
            }
            inlines.Add(run);
        }
    }

    // On the air every key goes straight to the transmitter instead of into the box; a key it cannot take is kept in
    // the box or, if the mode cannot send it, named under the line. Receiving in RTTY, the box shows capitals.
    private void OnTypeAheadText(object? sender, TextInputEventArgs e)
    {
        _spacePending = false;
        if (Vm?.Keyboard is not { } k || string.IsNullOrEmpty(e.Text)) return;
        if (k.OnAir)
        {
            k.TypeOnAir(e.Text);
            TypeAhead.CaretIndex = k.Typed.Length;
            e.Handled = true;
        }
        else if (k.IsRtty)
        {
            e.Text = e.Text.ToUpperInvariant();
        }
    }

    // Space pressed while another control had the keyboard: the type-ahead line now has it. Set until the space arrives
    // as text; a platform that sends no text for a handled key gets the space on key up instead.
    private bool _spacePending;

    /// <summary>
    /// PSK and RTTY: a key pressed while the keyboard is with another control on this view (a button, a row, the
    /// window). Typing belongs to the type-ahead line, so it takes the keyboard. Space must not click the focused button
    /// (which could key the radio again), and Backspace edits the line. Returns true when the key was taken.
    /// </summary>
    public bool TakeKey(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Vm?.Keyboard is not { Visible: true } k) return false;
        switch (e.Key)
        {
            case Key.Space:
                FocusTypeAhead();
                _spacePending = true;
                return true;
            case Key.Back:
                FocusTypeAhead();
                k.BackspaceAnywhere();
                TypeAhead.CaretIndex = k.Typed.Length;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Key up after <see cref="TakeKey"/>: a space that never arrived as text is typed now.</summary>
    public void TakeKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key != Key.Space || !_spacePending) return;
        _spacePending = false;
        TakeText(" ");
    }

    /// <summary>
    /// PSK and RTTY: text typed while another control had the keyboard goes to the type-ahead line (or, on the air, to
    /// the transmitter), and the line takes the keyboard. Returns true when the text was taken.
    /// </summary>
    public bool TakeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _spacePending = false;
        if (Vm?.Keyboard is not { Visible: true } k || text.Length == 0) return false;
        FocusTypeAhead();
        k.TypeAnywhere(text);
        TypeAhead.CaretIndex = k.Typed.Length;
        return true;
    }

    private void FocusTypeAhead()
    {
        if (!TypeAhead.IsFocused) TypeAhead.Focus();
    }

    private OperateViewModel? Vm => DataContext as OperateViewModel;

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is StationRowViewModel row) Vm?.RowClicked(row);
    }

    // Below 1100 px the right column drops under the left. On wide screens it grows so the map has room.
    private void Relayout(double width)
    {
        var narrow = width < 1100;
        var wide = width >= 1600;
        Root.ColumnDefinitions = narrow ? new ColumnDefinitions("*") : new ColumnDefinitions(wide ? "*,460" : "*,300");
        Root.RowDefinitions = narrow ? new RowDefinitions("*,Auto") : new RowDefinitions("*");
        Grid.SetColumn(RightColumn, narrow ? 0 : 1);
        Grid.SetRow(RightColumn, narrow ? 1 : 0);
        RightColumn.MaxHeight = narrow ? 320 : double.PositiveInfinity;
    }
}
