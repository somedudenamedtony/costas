// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Diagnostics;
using System.Runtime.InteropServices;
using static Ft8Client.UpdateSplash.Native;

namespace Ft8Client.UpdateSplash;

// The splash itself: a small borderless card in Costas's theme colours with the app icon, "Updating Costas", a status
// line and a moving bar, drawn with GDI. A timer (about 60 times a second) moves the bar and checks the hand-over.
internal static unsafe class SplashWindow
{
    private const string ClassName = "CostasUpdateSplash";
    private const int Width = 380;
    private const int Height = 112;
    private const int Pad = 20;
    private const int IconSize = 32;
    private const nuint TimerId = 1;
    private static readonly TimeSpan BarPeriod = TimeSpan.FromMilliseconds(1400);

    private static SplashOptions s_options = new();
    private static Func<(bool OldExited, bool Started, bool Ready)> s_poll = () => (true, false, false);
    private static readonly SplashProgress s_progress = new();
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();
    private static double s_scale = 1;
    private static nint s_icon;
    private static nint s_titleFont;
    private static nint s_statusFont;

    /// <summary>Shows the splash and runs until it is done. <paramref name="shown"/> is called once it is on screen.</summary>
    public static int Run(SplashOptions options, Func<(bool OldExited, bool Started, bool Ready)> poll, Action shown)
    {
        s_options = options;
        s_poll = poll;
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        var instance = GetModuleHandle(0);
        var classIcon = LoadImage(instance, AppIconId, IMAGE_ICON, 0, 0, 0x40 /* LR_DEFAULTSIZE */);
        fixed (char* cls = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                Size = (uint)sizeof(WNDCLASSEXW),
                Style = CS_DROPSHADOW,
                WndProc = &WndProc,
                Instance = instance,
                Icon = classIcon,
                IconSmall = classIcon,
                Cursor = LoadCursor(0, IDC_APPSTARTING),
                ClassName = cls,
            };
            if (RegisterClassEx(wc) == 0) return 2;
        }

        var scale = GetDpiForSystem() / 96.0;
        var (x, y, w, h) = Place(options.Center, scale);
        var hwnd = CreateWindowEx(WS_EX_APPWINDOW, ClassName, "Updating Costas", WS_POPUP, x, y, w, h, 0, 0, instance, 0);
        if (hwnd == 0) return 3;
        // The monitor the splash is on may have its own scale (a laptop beside a desk monitor).
        var dpi = GetDpiForWindow(hwnd);
        if (dpi != 0 && Math.Abs(dpi / 96.0 - scale) > 0.01)
        {
            scale = dpi / 96.0;
            (x, y, w, h) = Place(options.Center, scale);
            SetWindowPos(hwnd, 0, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        s_scale = scale;
        s_icon = LoadImage(instance, AppIconId, IMAGE_ICON, Px(IconSize), Px(IconSize), 0);
        s_titleFont = CreateFont(-Px(20), 0, 0, 0, FW_SEMIBOLD, 0, 0, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        s_statusFont = CreateFont(-Px(14), 0, 0, 0, FW_NORMAL, 0, 0, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        // Windows 11 rounds the corners like its own windows; earlier versions ignore this.
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, sizeof(int));

        Tick();
        ShowWindow(hwnd, SW_SHOWNORMAL);
        UpdateWindow(hwnd);
        SetForegroundWindow(hwnd);
        shown();
        SetTimer(hwnd, TimerId, 16, 0);
        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(msg);
            DispatchMessage(msg);
        }
        DeleteObject(s_titleFont);
        DeleteObject(s_statusFont);
        return 0;
    }

    private static int Px(double dip) => (int)Math.Round(dip * s_scale);

    // Centred where Costas's window was, or on the main screen's work area.
    private static (int X, int Y, int W, int H) Place((int X, int Y)? center, double scale)
    {
        var w = (int)Math.Round(Width * scale);
        var h = (int)Math.Round(Height * scale);
        int cx, cy;
        if (center is { } c)
        {
            (cx, cy) = c;
        }
        else if (SystemParametersInfo(SPI_GETWORKAREA, 0, out var work, 0))
        {
            cx = (work.Left + work.Right) / 2;
            cy = (work.Top + work.Bottom) / 2;
        }
        else
        {
            cx = cy = 400;
        }
        return (cx - w / 2, cy - h / 2, w, h);
    }

    private static SplashPhase Tick()
    {
        var (oldExited, started, ready) = s_poll();
        return s_progress.Update(s_clock.Elapsed, oldExited, started, ready);
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_ERASEBKGND:
                    return 1;
                case WM_PAINT:
                    Paint(hwnd);
                    return 0;
                case WM_TIMER:
                    if (Tick() == SplashPhase.Done) DestroyWindow(hwnd);
                    else InvalidateRect(hwnd, 0, false);
                    return 0;
                case WM_DESTROY:
                    KillTimer(hwnd, TimerId);
                    PostQuitMessage(0);
                    return 0;
            }
        }
        catch (Exception)
        {
            // Nothing may escape into Windows; a splash that fails just goes away.
            PostQuitMessage(1);
            return 0;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    // Drawn off screen and copied in one go, so the moving bar does not flicker.
    private static void Paint(nint hwnd)
    {
        var hdc = BeginPaint(hwnd, out var ps);
        try
        {
            GetClientRect(hwnd, out var rc);
            var w = rc.Right - rc.Left;
            var h = rc.Bottom - rc.Top;
            var mem = CreateCompatibleDC(hdc);
            var bmp = CreateCompatibleBitmap(hdc, w, h);
            var oldBmp = SelectObject(mem, bmp);
            Draw(mem, w, h);
            BitBlt(hdc, 0, 0, w, h, mem, 0, 0, SRCCOPY);
            SelectObject(mem, oldBmp);
            DeleteObject(bmp);
            DeleteDC(mem);
        }
        finally
        {
            EndPaint(hwnd, ps);
        }
    }

    private static void Draw(nint dc, int w, int h)
    {
        var o = s_options;
        Fill(dc, new RECT { Left = 0, Top = 0, Right = w, Bottom = h }, o.Background);
        var b = Math.Max(1, Px(1));
        Fill(dc, new RECT { Left = 0, Top = 0, Right = w, Bottom = b }, o.Border);
        Fill(dc, new RECT { Left = 0, Top = h - b, Right = w, Bottom = h }, o.Border);
        Fill(dc, new RECT { Left = 0, Top = 0, Right = b, Bottom = h }, o.Border);
        Fill(dc, new RECT { Left = w - b, Top = 0, Right = w, Bottom = h }, o.Border);

        var pad = Px(Pad);
        var textLeft = pad;
        if (s_icon != 0)
        {
            DrawIconEx(dc, pad, pad, s_icon, Px(IconSize), Px(IconSize), 0, 0, 0x3 /* DI_NORMAL */);
            textLeft = pad + Px(IconSize) + Px(14);
        }
        SetBkMode(dc, TRANSPARENT);
        var oldFont = SelectObject(dc, s_titleFont);
        SetTextColor(dc, ColorRef(o.Text));
        var title = new RECT { Left = textLeft, Top = Px(16), Right = w - pad, Bottom = Px(44) };
        DrawText(dc, "Updating Costas", -1, ref title, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS | DT_NOPREFIX);
        SelectObject(dc, s_statusFont);
        SetTextColor(dc, ColorRef(o.SecondaryText));
        var status = new RECT { Left = textLeft, Top = Px(44), Right = w - pad, Bottom = Px(66) };
        DrawText(dc, s_progress.Status(o.Version), -1, ref status, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS | DT_NOPREFIX);
        SelectObject(dc, oldFont);

        // An indeterminate bar: a segment sliding along a track, as Windows' own busy bars do.
        var top = h - pad - Px(4);
        var track = new RECT { Left = pad, Top = top, Right = w - pad, Bottom = top + Px(4) };
        Fill(dc, track, o.Border);
        var length = track.Right - track.Left;
        var segment = length * 3 / 10;
        var frac = s_clock.Elapsed.TotalMilliseconds % BarPeriod.TotalMilliseconds / BarPeriod.TotalMilliseconds;
        var start = track.Left - segment + (int)(frac * (length + segment));
        var bar = new RECT { Left = Math.Max(track.Left, start), Top = track.Top, Right = Math.Min(track.Right, start + segment), Bottom = track.Bottom };
        if (bar.Right > bar.Left) Fill(dc, bar, o.Accent);
    }

    private static void Fill(nint dc, RECT r, int rgb)
    {
        var brush = CreateSolidBrush(ColorRef(rgb));
        FillRect(dc, r, brush);
        DeleteObject(brush);
    }
}
