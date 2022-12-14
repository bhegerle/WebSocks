using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebSocks;

internal class Bridge
{
    private readonly DummyReceiver _dummyReceiver;
    private readonly WebSocket _webSocket;

    internal Bridge(WebSocket webSocket)
    {
        _webSocket = webSocket;
        _dummyReceiver = new DummyReceiver();
    }

    internal async Task Transit()
    {
        Socket s;


        while (true)
        {
            var sockRecv= _dummyReceiver.Receive();
        }
    }
}