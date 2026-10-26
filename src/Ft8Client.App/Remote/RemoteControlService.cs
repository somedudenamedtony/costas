// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net.NetworkInformation;
using System.Net.Sockets;
using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Core.Remote;
using Ft8Client.Core.Time;
using Ft8Client.Data.Secrets;
using Ft8Client.Data.Settings;
using Serilog;

namespace Ft8Client.App.Remote;

/// <summary>
/// Runs the remote page while Settings has it on: starts and stops the server, sends the state to the phones on every
/// change and once a second, and checks the controlling phone's link four times a second.
/// </summary>
public sealed class RemoteControlService : IAsyncDisposable
{
    private static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(250);

    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly IClock _clock;
    private readonly Func<RemoteContext> _context;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _switch = new(1, 1);
    private RemoteServer? _server;
    private (bool Enabled, int Port) _running;
    private volatile bool _dirty;
    private Task _loop = Task.CompletedTask;

    /// <summary>Creates the service.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settings">Settings.</param>
    /// <param name="secrets">Where the paired phones are kept.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="tuning">Band and Tx offset changes.</param>
    /// <param name="context">The log, QRZ, country file and bands the phone's state is built from.</param>
    /// <param name="power">The radio's power setting, which the phone shows and can change.</param>
    public RemoteControlService(Session session, AppSettingsAccessor settings, ISecretStore secrets, IClock clock, IRemoteTuning? tuning = null,
                                Func<RemoteContext>? context = null, RadioPowerService? power = null)
    {
        if (power is not null) power.Changed += () => _dirty = true;
        _session = session;
        _settings = settings;
        _clock = clock;
        _context = context ?? (() => new RemoteContext { Units = SettingsMapper.Units(settings.Current), QrzName = session.NameOf });
        Control = new RemoteControl(session, clock, DeviceTokens.Parse(secrets.Get(SecretNames.RemoteDevices)), t =>
        {
            if (t.Length == 0) secrets.Delete(SecretNames.RemoteDevices);
            else secrets.Set(SecretNames.RemoteDevices, t);
        }, tuning: tuning, power: power);
        Control.Changed += () =>
        {
            _dirty = true;
            Changed?.Invoke();
        };
    }

    /// <summary>Raised when the server starts or stops, a phone connects, or control changes hands.</summary>
    public event Action? Changed;

    /// <summary>The rules: pairing, control and commands.</summary>
    public RemoteControl Control { get; }

    /// <summary>True while the page is being served.</summary>
    public bool Running => _server is not null;

    /// <summary>Why the page is not being served although it is on (port taken), or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Number of phones connected now.</summary>
    public int Connected => _server?.Connected ?? 0;

    /// <summary>Starts serving if Settings has the page on.</summary>
    public void Start()
    {
        _settings.Changed += s => _ = ApplyAsync(s.Remote);
        _session.Changed += _ => _dirty = true;
        _loop = Task.Run(LoopAsync);
        _ = ApplyAsync(_settings.Current.Remote);
    }

    /// <summary>
    /// The addresses to open on a phone: one per local IPv4 address of this computer, the most likely first (an adapter
    /// with a default gateway, which is the home network, before virtual adapters such as WSL or Hyper-V).
    /// </summary>
    public IReadOnlyList<string> Urls()
    {
        var port = _settings.Current.Remote.Port;
        try
        {
            return [.. NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => (Props: n.GetIPProperties(), n.NetworkInterfaceType))
                .SelectMany(n => n.Props.UnicastAddresses.Select(a => (a.Address,
                    Gateway: n.Props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any)),
                    Physical: n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)))
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && LocalNetwork.Contains(a.Address) && !System.Net.IPAddress.IsLoopback(a.Address))
                .OrderByDescending(a => a.Gateway)
                .ThenByDescending(a => a.Physical)
                .Select(a => $"http://{a.Address}:{port}/")
                .Distinct()];
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    private async Task ApplyAsync(RemoteOptions o)
    {
        await _switch.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_running == (o.Enabled, o.Port) && (Running || !o.Enabled)) return;
            if (_server is not null)
            {
                var old = _server;
                _server = null;
                await old.DisposeAsync().ConfigureAwait(false);
                Control.DeskActivity();
            }
            _running = (o.Enabled, o.Port);
            Problem = null;
            if (o.Enabled && o.Port is < 1024 or > 65535)
            {
                Problem = $"Port {o.Port} cannot be used. Choose a port from 1024 to 65535.";
            }
            else if (o.Enabled)
            {
                var server = new RemoteServer(Control, StateFor);
                try
                {
                    await server.StartAsync(o.Port).ConfigureAwait(false);
                    _server = server;
                }
                catch (IOException ex)
                {
                    Log.Warning(ex, "Remote page could not start");
                    Problem = ex.Message;
                }
            }
        }
        finally
        {
            _switch.Release();
        }
        Changed?.Invoke();
    }

    private RemoteState StateFor(string deviceId) =>
        RemoteStateBuilder.Build(_session.Snapshot, _clock.UtcNow, _context(), Control.ControlFor(deviceId), Control.ViewingFor(deviceId)) with { Power = Control.PowerFor() };

    private async Task LoopAsync()
    {
        var lastSent = DateTime.MinValue;
        var lastConnected = 0;
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Pace, _cts.Token).ConfigureAwait(false);
                await Control.TickAsync().ConfigureAwait(false);
                var server = _server;
                if (server is null) continue;
                var now = _clock.UtcNow;
                if (_dirty || now - lastSent >= TimeSpan.FromSeconds(1))
                {
                    _dirty = false;
                    lastSent = now;
                    await server.BroadcastAsync().ConfigureAwait(false);
                }
                if (server.Connected != lastConnected)
                {
                    lastConnected = server.Connected;
                    Changed?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // The link-loss check must keep running whatever a broadcast throws.
                Log.Error(ex, "Remote loop");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        if (_server is not null) await _server.DisposeAsync().ConfigureAwait(false);
        _server = null;
        _cts.Dispose();
    }
}
