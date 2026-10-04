// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>Log view.</summary>
public partial class LogView : UserControl
{
    /// <summary>"Delete" or "Confirm delete".</summary>
    public static readonly IValueConverter DeleteText = new FuncValueConverter<bool, string>(b => b ? "Confirm delete" : "Delete");

    private static readonly FilePickerFileType Adif = new("ADIF") { Patterns = ["*.adi", "*.adif"] };

    /// <summary>Creates the view.</summary>
    public LogView() => InitializeComponent();

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LogViewModel vm || TopLevel.GetTopLevel(this)?.StorageProvider is not { } sp) return;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import ADIF", FileTypeFilter = [Adif] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) vm.Import(path);
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LogViewModel vm || TopLevel.GetTopLevel(this)?.StorageProvider is not { } sp) return;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export ADIF", SuggestedFileName = "log.adi", FileTypeChoices = [Adif] });
        if (file?.TryGetLocalPath() is { } path) vm.Export(path);
    }
}
