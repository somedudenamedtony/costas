// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Messages;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;
using Ft8Client.Data.Settings;
using Ft8Client.Integrations.Wsjtx;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// WSJT-X UDP interop, outbound only: heartbeat every 15 s, status on change, decodes, clear on band change, logged
/// contacts (both forms) and close on exit. Lets GridTracker, JTAlert and loggers follow this app unchanged.
/// </summary>
public sealed class UdpInteropService : IDisposable
{
    /// <summary>The Id field in every datagram.</summary>
    public const string ClientId = AppInfo.Id;

    private static readonly TimeSpan Pulse = TimeSpan.FromSeconds(15);

    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly bool _offAir;
    private readonly Lock _gate = new();
    private readonly Timer _heartbeat;
    private WsjtxUdpBroadcaster? _udp;
    private (bool Enabled, string Address, int Port) _target;
    private WsjtxStatus? _lastStatus;
    private bool _warned;

    /// <summary>Creates the service. <paramref name="offAir"/> marks decodes as from recordings (simulation).</summary>
    public UdpInteropService(Session session, AppSettingsAccessor settings, bool offAir)
    {
        _session = session;
        _settings = settings;
        _offAir = offAir;
        _heartbeat = new Timer(_ => Send(WsjtxMessages.Heartbeat(ClientId, AppInfo.Version, string.Empty)));
    }

    /// <summary>Opens the socket and starts sending.</summary>
    public void Start()
    {
        Reconfigure(_settings.Current);
        _settings.Changed += Reconfigure;
        _session.Changed += OnChanged;
        _session.SlotDecoded += OnSlotDecoded;
        _session.BandChanged += OnBandChanged;
        _heartbeat.Change(TimeSpan.Zero, Pulse);
    }

    /// <summary>Sends a logged contact as QSO Logged and Logged ADIF.</summary>
    public void OnLogged(QsoRecord q)
    {
        Send(WsjtxMessages.QsoLogged(ClientId, new WsjtxQso
        {
            TimeOnUtc = q.QsoDateOn, TimeOffUtc = q.QsoDateOff ?? q.QsoDateOn, DxCall = q.Call, DxGrid = q.Gridsquare ?? string.Empty,
            FrequencyHz = q.FreqHz ?? 0, Mode = q.Submode ?? q.Mode, ReportSent = q.RstSent ?? string.Empty,
            ReportReceived = q.RstRcvd ?? string.Empty, TxPower = q.TxPwr ?? string.Empty, Name = q.Name ?? string.Empty,
            MyCall = q.StationCallsign ?? string.Empty, MyGrid = q.MyGridsquare ?? string.Empty,
        }));
        Send(WsjtxMessages.LoggedAdif(ClientId, AdifWriter.Document([AdifMapper.ToAdif(q)], DateTime.UtcNow)));
    }

    private void Reconfigure(AppSettings s)
    {
        var t = (s.Udp.Enabled, s.Udp.Address, s.Udp.Port);
        lock (_gate)
        {
            if (t == _target && (_udp is not null || !t.Enabled)) return;
            _target = t;
            _udp?.Dispose();
            _udp = null;
            _lastStatus = null;
            _warned = false;
            if (!t.Enabled) return;
            try
            {
                _udp = new WsjtxUdpBroadcaster(t.Address, t.Port);
            }
            catch (Exception ex) when (ex is FormatException or System.Net.Sockets.SocketException or ArgumentException)
            {
                Log.Warning("UDP interop off: cannot send to {Address}:{Port}: {Error}", t.Address, t.Port, ex.Message);
            }
        }
        if (t.Enabled) Send(WsjtxMessages.Heartbeat(ClientId, AppInfo.Version, string.Empty));
    }

    private void OnChanged(SessionSnapshot s)
    {
        var c = s.Contact;
        var status = new WsjtxStatus
        {
            DialHz = s.DialHz, Mode = ModeInfo.Name(s.Mode), DxCall = c?.DxCall ?? string.Empty,
            Report = c?.ReportSent is { } rs ? Report.Format(rs) : string.Empty, TxEnabled = c is not null || s.CallingCq,
            Transmitting = s.Transmitting, RxDf = s.TxOffsetHz, TxDf = s.TxOffsetHz, DeCall = s.MyCall, DeGrid = s.MyGrid,
            DxGrid = c?.DxGrid ?? string.Empty, TrPeriodSeconds = (int)ModeInfo.SlotLength(s.Mode).TotalSeconds,
            ConfigurationName = _settings.Current.Profile.Name, TxMessage = s.TransmittingMessage ?? string.Empty,
        };
        lock (_gate)
        {
            if (status == _lastStatus) return;
            _lastStatus = status;
        }
        Send(WsjtxMessages.Status(ClientId, status));
    }

    private void OnSlotDecoded(DateTime slot, Mode mode, IReadOnlyList<Decode> decodes)
    {
        var name = ModeInfo.Name(mode);
        foreach (var d in decodes)
            Send(WsjtxMessages.Decode(ClientId, new WsjtxDecode(slot, d.Snr, d.Dt, d.OffsetHz, name, d.Text, d.LowConfidence, _offAir)));
    }

    private void OnBandChanged() => Send(WsjtxMessages.Clear(ClientId));

    private void Send(byte[] datagram)
    {
        WsjtxUdpBroadcaster? udp;
        lock (_gate) udp = _udp;
        if (udp is null || udp.Send(datagram)) return;
        if (_warned) return;
        _warned = true;
        Log.Warning("UDP interop send failed: {Error}", udp.LastError);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.Changed -= Reconfigure;
        _session.Changed -= OnChanged;
        _session.SlotDecoded -= OnSlotDecoded;
        _session.BandChanged -= OnBandChanged;
        _heartbeat.Dispose();
        Send(WsjtxMessages.Clear(ClientId));
        Send(WsjtxMessages.Close(ClientId));
        lock (_gate)
        {
            _udp?.Dispose();
            _udp = null;
        }
    }
}
