// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ft8Client.Core.Remote;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Ft8Client.App.Remote;

/// <summary>
/// Serves the remote page on the home network: the page itself, pairing, and one WebSocket per phone that carries the
/// station's state down and the operator's taps up. Refuses any address outside the local network.
/// </summary>
public sealed class RemoteServer : IAsyncDisposable
{
    /// <summary>Largest message accepted from a phone.</summary>
    public const int MaxMessageBytes = 4096;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

    private readonly RemoteControl _control;
    private readonly Func<string, RemoteState> _state;
    private readonly ConcurrentDictionary<Guid, Client> _clients = new();
    private WebApplication? _app;

    /// <summary>Creates the server.</summary>
    /// <param name="control">The rules.</param>
    /// <param name="state">Builds the state for a phone, by device id.</param>
    public RemoteServer(RemoteControl control, Func<string, RemoteState> state)
    {
        _control = control;
        _state = state;
    }

    /// <summary>Number of phones connected and signed in.</summary>
    public int Connected => _clients.Count;

    /// <summary>Starts listening on <paramref name="port"/> on every interface. Throws IOException when the port is taken.</summary>
    public async Task StartAsync(int port)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.ListenAnyIP(port);
            k.AddServerHeader = false;
            k.Limits.MaxRequestBodySize = MaxMessageBytes;
            k.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
        });
        var app = builder.Build();
        app.Use(async (ctx, next) =>
        {
            // Only this home network: a router forwarding the port to the internet must not expose the station.
            if (!LocalNetwork.Contains(ctx.Connection.RemoteIpAddress))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self' ws: wss:; " +
                "manifest-src 'self'; img-src 'self'; frame-ancestors 'none'";
            await next(ctx).ConfigureAwait(false);
        });
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });
        app.MapGet("/", (Delegate)ServePage);
        foreach (var (path, type) in Files) app.MapGet("/" + path, () => ServeFile(path, type));
        app.MapPost("/api/pair", (Delegate)Pair);
        app.Map("/ws", AcceptAsync);
        try
        {
            await app.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException)
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw new IOException($"Port {port} is in use by another program. Choose another port.", ex);
        }
        _app = app;
        Log.Information("Remote page listening on port {Port}", port);
    }

    /// <summary>Sends the latest state to every connected phone.</summary>
    public async Task BroadcastAsync()
    {
        foreach (var c in _clients.Values)
        {
            // "Forget paired phones" must cut off a phone that is already connected, not only stop it signing in again.
            if (_control.SignIn(c.Token) is null)
            {
                await CloseAsync(c, "unpaired").ConfigureAwait(false);
                continue;
            }
            await SendAsync(c, new { type = "state", state = _state(c.DeviceId) }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var app = _app;
        _app = null;
        foreach (var c in _clients.Values) c.Socket.Abort();
        _clients.Clear();
        if (app is not null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await app.StopAsync(cts.Token).ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    // What a phone needs to add the page to its home screen as an app.
    private static readonly (string Path, string Type)[] Files =
    [
        ("manifest.webmanifest", "application/manifest+json"),
        ("icon-180.png", "image/png"),
        ("icon-192.png", "image/png"),
        ("icon-512.png", "image/png"),
    ];

    private static IResult ServeFile(string path, string type)
    {
        using var s = typeof(RemoteServer).Assembly.GetManifestResourceStream("Ft8Client.App.Remote." + path);
        if (s is null) return Results.NotFound();
        using var m = new MemoryStream();
        s.CopyTo(m);
        return Results.Bytes(m.ToArray(), type);
    }

    private static IResult ServePage()
    {
        using var s = typeof(RemoteServer).Assembly.GetManifestResourceStream("Ft8Client.App.Remote.index.html");
        if (s is null) return Results.NotFound();
        using var r = new StreamReader(s);
        var html = r.ReadToEnd();
        return Results.Text(html, "text/html; charset=utf-8");
    }

    // No code (owner decision, 2026-10-25): any device on the home network is given a token, which only identifies it
    // so control stays with that phone. Addresses outside the local network are still refused.
    private IResult Pair(HttpContext ctx)
    {
        var token = _control.Pair();
        Log.Information("Phone connected for the first time from {Address}", ctx.Connection.RemoteIpAddress);
        return Results.Json(new { token });
    }

    private async Task AcceptAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest || !SameOrigin(ctx.Request))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        using var socket = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var buffer = new byte[MaxMessageBytes];

        // The first message signs in with the phone's token.
        using var helloCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        helloCts.CancelAfter(HelloTimeout);
        var hello = await ReceiveAsync(socket, buffer, helloCts.Token).ConfigureAwait(false);
        var token = hello is null ? null : Parse<HelloMessage>(hello)?.Token;
        var deviceId = _control.SignIn(token);
        if (deviceId is null)
        {
            await CloseAsync(socket, "unpaired").ConfigureAwait(false);
            return;
        }
        var client = new Client(socket, deviceId, token!);
        var id = Guid.NewGuid();
        _clients[id] = client;
        try
        {
            await _control.HandleAsync(deviceId, new RemoteCommand("ping")).ConfigureAwait(false);
            await SendAsync(client, new { type = "state", state = _state(deviceId) }).ConfigureAwait(false);
            while (socket.State == WebSocketState.Open)
            {
                var text = await ReceiveAsync(socket, buffer, ctx.RequestAborted).ConfigureAwait(false);
                if (text is null) break;
                var cmd = Parse<RemoteCommand>(text);
                if (cmd is null || string.IsNullOrEmpty(cmd.Type)) continue;
                if (_control.SignIn(token) is null)
                {
                    await CloseAsync(client, "unpaired").ConfigureAwait(false);
                    break;
                }
                string? note;
                RemoteState? state = null;
                try
                {
                    note = await _control.HandleAsync(deviceId, cmd).ConfigureAwait(false);
                    if (cmd.Type != "ping") state = _state(deviceId);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    // Also covers reading the log while a contact is being written: the next broadcast catches up.
                    Log.Warning(ex, "Remote command {Type} failed", cmd.Type);
                    note = "That did not work at the station.";
                }
                if (cmd.Type == "ping") await SendAsync(client, new { type = "pong" }).ConfigureAwait(false);
                else if (state is not null) await SendAsync(client, new { type = "state", state, note }).ConfigureAwait(false);
                else await SendAsync(client, new { type = "note", note }).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
        {
        }
        finally
        {
            _clients.TryRemove(id, out _);
        }
    }

    // Browsers always send Origin on a WebSocket; a page on another site must not drive the station.
    private static bool SameOrigin(HttpRequest req)
    {
        var origin = req.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true;
        return Uri.TryCreate(origin, UriKind.Absolute, out var u)
               && string.Equals(u.Authority, req.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReceiveAsync(WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        var count = 0;
        while (true)
        {
            var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), ct).ConfigureAwait(false);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            count += r.Count;
            if (r.EndOfMessage) return r.MessageType == WebSocketMessageType.Text ? Encoding.UTF8.GetString(buffer, 0, count) : string.Empty;
            if (count >= buffer.Length)
            {
                await CloseAsync(socket, "too big").ConfigureAwait(false);
                return null;
            }
        }
    }

    private static T? Parse<T>(string text)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task SendAsync(Client c, object message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        await c.SendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (c.Socket.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await c.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            c.Socket.Abort();
        }
        finally
        {
            c.SendLock.Release();
        }
    }

    private static async Task CloseAsync(Client c, string why)
    {
        // Only the closing frame is sent here: the phone's receive loop may be waiting on this socket and sees the reply.
        await c.SendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            if (c.Socket.State == WebSocketState.Open)
                await c.Socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, why, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            c.Socket.Abort();
        }
        finally
        {
            c.SendLock.Release();
        }
    }

    private static async Task CloseAsync(WebSocket socket, string why)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, why, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
        {
        }
    }


    private sealed record HelloMessage(string? Type, string? Token);

    private sealed class Client(WebSocket socket, string deviceId, string token)
    {
        public WebSocket Socket { get; } = socket;

        public string DeviceId { get; } = deviceId;

        public string Token { get; } = token;

        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}
