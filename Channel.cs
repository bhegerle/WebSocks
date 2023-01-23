using System.Net.WebSockets;

namespace WebStunnel {
    internal class Channel : IDisposable {
        private readonly WebSocket _ws;
        private readonly Codec _codec;
        private readonly SemaphoreSlim _mutex;

        internal Channel(WebSocket ws, Codec codec) {
            _ws = ws;
            _codec = codec;

            _mutex = new SemaphoreSlim(1);
        }

        public void Dispose() {
            _ws?.Dispose();
        }

        internal async Task HandshakeCheck(CancellationToken token) {
            await _mutex.WaitAsync(token);
            try {
                if (_codec.State == CodecState.Init) {
                    Console.WriteLine("init handshake");

                    var seg = new byte[Codec.InitMessageSize];
                    var sendSeg = _codec.InitHandshake(seg);
                    await WsSend(sendSeg, token);

                    var recvSeg = await WsRecv(seg, token);
                    _codec.VerifyHandshake(recvSeg);

                    Console.WriteLine("completed handshake");
                }
            } finally {
                _mutex.Release();
            }
        }

        internal async Task Send(ArraySegment<byte> seg, CancellationToken token) {
            await _mutex.WaitAsync(token);
            try {
                seg = _codec.AuthMessage(seg);
            } finally {
                _mutex.Release();
            }

            await WsSend(seg, token);
        }

        internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg, CancellationToken token) {
            seg = await WsRecv(seg, token);

            await _mutex.WaitAsync(token);
            try {
                seg = _codec.VerifyMessage(seg);
            } finally {
                _mutex.Release();
            }

            return seg;
        }

        private async Task<ArraySegment<byte>> WsRecv(ArraySegment<byte> seg, CancellationToken token) {
            var init = seg;
            var n = 0;

            while (seg.Count > 0) {
                var recv = await _ws.ReceiveAsync(seg, token);
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
            await _ws.SendAsync(seg,
                WebSocketMessageType.Binary,
                WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
                token);
        }
    }
}
