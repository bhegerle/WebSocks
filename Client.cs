using System.Net.WebSockets;

namespace WebSocks;

internal class Client
{
    private readonly Uri _address;

    internal Client(string address)
    {
        _address = new Uri(address);
    }

    internal async Task Start()
    {
        var client=new ClientWebSocket();

        var cts=new CancellationTokenSource();
        await client.ConnectAsync(_address, cts.Token);

        Console.WriteLine($"connected to {_address}");

        Thread.Sleep(10000);
    }
}