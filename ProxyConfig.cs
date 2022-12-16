﻿using System.Net;
using System.Net.WebSockets;

namespace WebStunnel;

public record ProxyConfig
{
    public bool UseSystemProxy { get; init; }
    public string HttpProxy { get; init; }

    public void Configure(ClientWebSocket ws, Uri uri)
    {
        if (UseSystemProxy)
        {
            var proxyUri = WebRequest.GetSystemWebProxy().GetProxy(uri);

            if (proxyUri != null)
            {
                ws.Options.Proxy = new WebProxy(proxyUri);
                Console.WriteLine($"connecting to {uri} through WebProxy {proxyUri}");
            }
        }
        else if (HttpProxy != null)
        {
            var proxyUri = new Uri(HttpProxy);
            ws.Options.Proxy = new WebProxy(proxyUri);
            Console.WriteLine($"connecting through WebProxy {proxyUri}");
        }
    }
}