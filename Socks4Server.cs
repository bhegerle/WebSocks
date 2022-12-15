﻿using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebSocks;

internal class Socks4Server
{
    private readonly Config _config;

    internal Socks4Server(Config config)
    {
        config.ListenUri.CheckUri("listen", "socks4");
        config.TunnelUri.CheckUri("bridge", "ws");
        _config = config;
    }

    private EndPoint EndPoint => _config.ListenUri.EndPoint();
    private Uri TunnelUri => _config.TunnelUri;
    private SystemProxyConfig SystemProxyConfig => _config.SystemProxyConfig;

    internal async Task Start()
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(EndPoint);
        socket.Listen();
        Console.WriteLine($"listening on {EndPoint}");

        while (true)
        {
            var s = await socket.AcceptAsync();
            Console.WriteLine($"connection from {s.RemoteEndPoint}");

            var cts = new CancellationTokenSource();

            var ws = new ClientWebSocket();
            SystemProxyConfig.Configure(ws, TunnelUri);

            await ws.ConnectAsync(TunnelUri, cts.Token);
            Console.WriteLine($"bridging through {TunnelUri}");

            var b = new Bridge(s, ws);
            await b.Transit(cts.Token);
        }
    }
}