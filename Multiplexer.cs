using System.Net.Sockets;

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

            while (true) {
                using var cts = LinkTimeout(token, Config.IdleTimeout);
                var msg = await tunnel.Receive(seg, cts.Token);

                ulong id = 0; // fixme get actual id

                var sock = await sockMap.GetSocket(id);
                await sock.Send(msg, cts.Token);
            }
        }

        private async Task SocketReceive(CancellationToken token) {
            var taskMap = new Dictionary<ulong, Task>();

            while (true) {
                var snap = await sockMap.Snapshot();

                foreach (var (id, s) in snap.Sockets) {
                    if (!taskMap.ContainsKey(id))
                        taskMap.Add(id, SocketReceive(id, s));
                }

                await WhenAnyCancelled(token, snap.SnapshotToken);

                if (snap.SnapshotToken.IsCancellationRequested) {
                    var completed = taskMap
                        .Where(e => e.Value.IsCompleted)
                        .ToList();

                    foreach (var e in completed)
                        taskMap.Remove(e.Key);
                }
            }
        }

        private async Task SocketReceive(ulong id, Socket s) {
            var seg = NewSeg();

            try {
                while (true) {
                    using var cts = new CancellationTokenSource();
                    cts.CancelAfter(Config.IdleTimeout);

                    var msg = s.Receive(seg, cts.Token);

                    // add id

                    using var cts2=new CancellationTokenSource();
                    cts2.CancelAfter(Config.Timeout);
                    await tunnel.Send(seg, cts2.Token);
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
