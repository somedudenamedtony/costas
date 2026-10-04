// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>
/// Holds back a re-ranked list while the operator is pointing at it or has just clicked it, so a row never moves
/// under the pointer or within 300 ms of a click (02-ui-spec.md, "Next in line"). A held list shows the
/// "List updated" pill and is applied on click or when the pointer leaves.
/// </summary>
/// <typeparam name="T">The list payload.</typeparam>
public sealed class LineStabilizer<T>(Func<DateTime> now) where T : class
{
    /// <summary>How long after a click the list stays still.</summary>
    public static readonly TimeSpan ClickHold = TimeSpan.FromMilliseconds(300);

    private DateTime _lastClick = DateTime.MinValue;
    private bool _pointerOver;

    /// <summary>The list on screen.</summary>
    public T? Applied { get; private set; }

    /// <summary>A newer list waiting to be applied.</summary>
    public T? Pending { get; private set; }

    /// <summary>True when a newer list is waiting (show the pill).</summary>
    public bool HasPending => Pending is not null;

    /// <summary>Raised when <see cref="Applied"/> changes.</summary>
    public event Action<T>? AppliedChanged;

    /// <summary>Offers a new list. Returns true if it was applied now.</summary>
    public bool Offer(T list)
    {
        if (Applied is null || (!_pointerOver && now() - _lastClick >= ClickHold))
        {
            Apply(list);
            return true;
        }
        Pending = list;
        return false;
    }

    /// <summary>The pointer entered the list.</summary>
    public void PointerEntered() => _pointerOver = true;

    /// <summary>The pointer left the list: apply what is waiting.</summary>
    public void PointerExited()
    {
        _pointerOver = false;
        if (Pending is not null && now() - _lastClick >= ClickHold) Apply(Pending);
    }

    /// <summary>The operator clicked a row.</summary>
    public void Clicked() => _lastClick = now();

    /// <summary>Applies a waiting list once the click hold has passed (call from a timer).</summary>
    public void Tick()
    {
        if (Pending is not null && !_pointerOver && now() - _lastClick >= ClickHold) Apply(Pending);
    }

    /// <summary>The pill was clicked: apply now.</summary>
    public void ApplyPending()
    {
        if (Pending is not null) Apply(Pending);
    }

    private void Apply(T list)
    {
        Applied = list;
        Pending = null;
        AppliedChanged?.Invoke(list);
    }
}
