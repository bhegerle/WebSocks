using System.Net.Sockets;
using static WebStunnel.Timeouts;

namespace WebStunnel {
    internal class Multiplexer : IDisposable {
        private readonly Tunnel tunnel;
        private readonly ISocketMap sockMap;

        internal Multiplexer(Tunnel tunnel, ISocketMap sockMap) {
            this.tunnel = tunnel;
            this.sockMap = sockMap;
        }

        internal async Task Multiplex(CancellationToken token) {

        }

        public void Dispose() {
            tunnel.Dispose();
            sockMap.Dispose();
        }

        private async Task TunnelReceive(CancellationToken token) {
            var seg = NewSeg();
            var idBuf = new byte[sizeof(ulong)].AsSegment();

            while (true) {
                using var cts = LinkTimeout(token, Config.IdleTimeout);
                var msg = await tunnel.Receive(seg, cts.Token);

                var f = new Frame(msg, idBuf.Count, false);
                var id = BitConverter.ToUInt64(f.Suffix);
                var sock = await sockMap.GetSocket(id);

                using var sendTimeout = Timeout();
                await sock.Send(f.Message, sendTimeout.Token);
            }
        }

        private async Task SocketReceive(CancellationToken token) {
            var taskMap = new Dictionary<ulong, Task>();

            while (true) {
                var snap = await sockMap.Snapshot();

                var newInSnap = snap.Sockets.Keys.Except(taskMap.Keys);
                foreach (var sid in newInSnap) {
                    taskMap.Add(sid, SocketReceive(sid, snap.Sockets[sid]));
                    Console.WriteLine($"multiplexing connection {sid}");
                }

                var dropFromSnap = taskMap.Keys.Except(snap.Sockets.Keys);
                foreach (var tid in dropFromSnap) {
                    var t = taskMap[tid];
                    if (await t.DidCompleteWithin(TimeSpan.FromMilliseconds(1)))
                        taskMap.Remove(tid);
                }

                await snap.ReplacementSnapshotAvailable.UntilCancelled(token);
            }
        }

        private async Task SocketReceive(ulong id, Socket s) {
            var seg = NewSeg();
            var idBuf = BitConverter.GetBytes(id).AsSegment();

            try {
                while (true) {
                    using var recvTimeout = IdleTimeout();
                    var msg = await s.Receive(seg, recvTimeout.Token);

                    var f = new Frame(msg, idBuf.Count, true);
                    idBuf.CopyTo(f.Suffix);

                    using var sendTimeout = Timeout();
                    await tunnel.Send(f.Complete, sendTimeout.Token);
                }
            } catch {
                await sockMap.RemoveSocket(id);
            }
        }

        private static ArraySegment<byte> NewSeg() {
            return new byte[1024 * 1024];
        }

        static CancellationTokenSource LinkTimeout(CancellationToken token, TimeSpan timeout) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeout);
            return cts;
        }

        static async Task WhenAnyCancelled(params CancellationToken[] tokens) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(tokens);
            try {
                await Task.Delay(TimeSpan.MaxValue, cts.Token);
            } catch (TaskCanceledException) {
            }
        }
    }
}
