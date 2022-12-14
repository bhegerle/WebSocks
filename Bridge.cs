namespace WebSocks;

internal class Bridge
{
    private readonly Func<Task<byte[]>> _recv;
    private readonly Func<byte[], Task> _send;

    internal Bridge(Func<Task<byte[]>> recv, Func<byte[], Task> send)
    {
        _recv = recv;
        _send = send;
    }

    internal async Task Transit()
    {
        for (var i = 0; i < 10; i++)
        {
            var b = await _recv();
            await _send(b);
        }
    }
}