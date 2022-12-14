namespace shock;

internal class DummyReceiver
{
    private int i;

    internal async Task<byte[]> Receive()
    {
        if (i < 10)
        {
            i++;
            var b = BitConverter.GetBytes(i);
            return b;
        }

        return null;
    }
}