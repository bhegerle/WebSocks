using System.Net.WebSockets;

namespace WebStunnel {
    internal class Channel : IDisposable {
        private readonly WebSocket ws;
        private readonly Codec codec;
        private readonly SemaphoreSlim mutex;

        internal Channel(WebSocket ws, Codec codec) {
            this.ws = ws;
            this.codec = codec;

            mutex = new SemaphoreSlim(1);
        }

        public void Dispose() {
            Console.WriteLine("disposing WebSocket");
            ws?.Dispose();
        }

        internal async Task HandshakeCheck(CancellationToken token) {
            await mutex.WaitAsync(token);
            try {
                if (codec.State == CodecState.Init) {
                    Console.WriteLine("    init handshake");

                    var seg = new byte[Codec.InitMessageSize];
                    var sendSeg = codec.InitHandshake(seg);
                    await WsSend(sendSeg, token);

                    var recvSeg = await WsRecv(seg, token);
                    codec.VerifyHandshake(recvSeg);

                    Console.WriteLine("    completed handshake");
                }
            } catch (Exception ex) {
                await Log.Warn("handshake failed", ex);
                throw;
            } finally {
                mutex.Release();
            }
        }

        internal async Task Send(ArraySegment<byte> seg, CancellationToken token) {
            var n = seg.Count;

            await mutex.WaitAsync(token);
            try {
                seg = codec.AuthMessage(seg);
            } finally {
                mutex.Release();
            }

            Console.WriteLine($"sending {n} ({seg.Count} encoded)");

            await WsSend(seg, token);
        }

        internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg, CancellationToken token) {
            seg = await WsRecv(seg, token);

            var n = seg.Count;

            await mutex.WaitAsync(token);
            try {
                seg = codec.VerifyMessage(seg);
            } finally {
                mutex.Release();
            }

            Console.WriteLine($"received {seg.Count} ({n} encoded)");

            return seg;
        }

        private async Task<ArraySegment<byte>> WsRecv(ArraySegment<byte> seg, CancellationToken token) {
            var init = seg;
            var n = 0;

            while (seg.Count > 0) {
                var recv = await ws.ReceiveAsync(seg, token);
                if (recv.MessageType != WebSocketMessageType.Binary) {
                    throw new Exception("non-binary message received");
                }

                n += recv.Count;
                seg = seg[recv.Count..];

                if (recv.EndOfMessage) {
                    return init[..n];
                }

                Console.WriteLine($"ws partial: buffered {recv.Count} bytes");
            }

            throw new Exception("message exceeds segment");
        }

        private async Task WsSend(ArraySegment<byte> seg, CancellationToken token) {
            await ws.SendAsync(seg,
                WebSocketMessageType.Binary,
                WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
                token);
        }
    }
}
