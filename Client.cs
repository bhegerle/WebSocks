using System.Net.WebSockets;

namespace WebSocks;

internal class Client
{
    private readonly Uri _address;
    readonly CancellationTokenSource _cts;

    internal Client(string address)
    {
        _address = new Uri(address);
        _cts= new CancellationTokenSource();
    }

    internal async Task Start()
    {
        var client=new ClientWebSocket();

        await client.ConnectAsync(_address, _cts.Token);

        Console.WriteLine($"connected to {_address}");

        var buffer = new byte[] { 1, 2, 3, 4 };
         await client.SendAsync(buffer, WebSocketMessageType.Binary, true, _cts.Token);

         Console.WriteLine($"sent");

        Thread.Sleep(10000);
    }
}