// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;

namespace Ft8Client.App.Views;

/// <summary>
/// Asks before an action that would end the contact or CQ in progress. Closes with true only for the action button;
/// Enter, Esc and closing the window keep the contact.
/// </summary>
public partial class ConfirmWindow : Window
{
    /// <summary>Creates the window (for the designer).</summary>
    public ConfirmWindow()
        : this(string.Empty, "OK")
    {
    }

    /// <summary>Creates the window with a question and the action button's text.</summary>
    public ConfirmWindow(string question, string action)
    {
        InitializeComponent();
        Question.Text = question;
        Yes.Content = action;
        Yes.Click += (_, _) => Close(true);
        No.Click += (_, _) => Close(false);
    }
}
