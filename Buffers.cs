namespace WebStunnel;

internal static class Buffers {
    private const int size = 1024 * 1024;
    private static object sync;
    private static Stack<byte[]> stack;

    static Buffers() {
        sync = new object();
        stack = new Stack<byte[]>();
    }

    internal static ArraySegment<byte> New() {
        lock (sync) {
            return stack.Count > 0 ? stack.Pop() : Alloc();
        }
    }

    internal static SocketSegment New(SocketId id) {
        var seg = New();
        seg = seg[..^Message.Data.SuffixSize];
        return new SocketSegment(id, seg);
    }

    internal static void Return(ArraySegment<byte> segment) {
        var a = segment.Array;
        if (a != null && a.Length == size)
            lock (sync) {
                stack.Push(segment.Array);
            }
    }

    private static byte[] Alloc() {
        return new byte[size];
    }
}