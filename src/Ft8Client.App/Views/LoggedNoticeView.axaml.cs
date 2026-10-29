// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>The "Logged" notice; the pointer resting on it keeps it open, and a click opens its editor.</summary>
public partial class LoggedNoticeView : UserControl
{
    private LoggedNoticeViewModel? _vm;

    /// <summary>Creates the control.</summary>
    public LoggedNoticeView()
    {
        InitializeComponent();
        Card.PointerEntered += (_, _) => _vm?.Hover(true);
        Card.PointerExited += (_, _) => _vm?.Hover(false);
        Summary.PointerPressed += (_, e) =>
        {
            _vm?.EditCommand.Execute(null);
            e.Handled = true;
        };
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as LoggedNoticeViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        };
    }

    // The call box takes the keyboard when the editor opens, so a correction can be typed at once.
    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoggedNoticeViewModel.IsEditing) && _vm is { IsEditing: true })
            Dispatcher.UIThread.Post(() => CallBox.Focus());
    }
}
