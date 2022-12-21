using System.Security.Cryptography;
using System.Text;
using static System.Security.Cryptography.HMACSHA512;

namespace WebStunnel;

internal class Codec {
    private const int HashSize = 512 / 8;
    private readonly ArraySegment<byte> _key, _auth, _verify, _tmp;
    private readonly byte _protoByte;

    private State _state;

    internal Codec(ProtocolByte protoByte, Config config) {
        if (string.IsNullOrEmpty(config.Key))
            throw new Exception("key required");

        _protoByte = (byte)protoByte;

        _key = SHA512.HashData(Encoding.UTF8.GetBytes(config.Key));

        _auth = new byte[HashSize];
        _verify = new byte[HashSize];
        _tmp = new byte[HashSize];
    }

    internal static int InitMessageSize => HashSize;

    internal ArraySegment<byte> InitHandshake(ArraySegment<byte> seg) {
        try {
            Transition(State.Init, State.Handshake);

            if (seg.Count != InitMessageSize)
                throw new Exception("wrong size for init auth message");

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(seg);

            return seg;
        } catch {
            SetError();
            throw;
        }
    }

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg) {
        try {
            CheckState(State.Active);
            return AuthMsg(seg);
        } catch {
            SetError();
            throw;
        }
    }

    internal void VerifyHandshake(ArraySegment<byte> seg) {
        try {
            Transition(State.Handshake, State.Active);

            if (seg.Count != InitMessageSize)
                throw new Exception("wrong size for init handshake message");

            var acat = Cat(_protoByte, _auth, _verify);
            var vcat = Cat((byte)~_protoByte, _verify, _auth);

            HashData(_key, acat, _auth);
            HashData(_key, vcat, _verify);
        } catch {
            SetError();
            throw;
        }
    }

    internal ArraySegment<byte> VerifyMessage(ArraySegment<byte> seg) {
        try {
            CheckState(State.Active);
            return VerifyMsg(seg);
        } catch {
            SetError();
            throw;
        }
    }

    private ArraySegment<byte> AuthMsg(ArraySegment<byte> seg) {
        var msg = new Frame(seg, true);

        _auth.CopyTo(msg.Hmac);
        HashData(_key, msg.Complete, _auth);

        _auth.CopyTo(msg.Hmac);

        return msg.Complete;
    }

    private ArraySegment<byte> VerifyMsg(ArraySegment<byte> seg) {
        var msg = new Frame(seg, false);

        msg.Hmac.CopyTo(_tmp);
        _verify.CopyTo(msg.Hmac);
        HashData(_key, msg.Complete, _verify);

        if (!Utils.ConjEqual(_verify, _tmp))
            throw new Exception("invalid HMAC");

        return msg.Message;
    }

    private void CheckState(State expected) {
        if (_state != expected)
            throw new Exception("invalid codec state");
    }

    private void Transition(State expected, State next) {
        CheckState(expected);
        _state = next;
    }

    private void SetError() {
        _state = State.Error;
    }

    private static byte[] Cat(byte b, ArraySegment<byte> seg0, ArraySegment<byte> seg1) {
        var cat = new byte[1 + seg0.Count + seg1.Count];
        cat[0] = b;
        seg0.CopyTo(cat, 1);
        seg1.CopyTo(cat, seg0.Count + 1);
        return cat;
    }

    private enum State {
        Init,
        Handshake,
        Active,
        Error
    }

    private readonly struct Frame {
        internal Frame(ArraySegment<byte> x, bool extend) {
            if (extend)
                x = x.Array.AsSegment(x.Offset, x.Count + HashSize);

            Complete = x;
        }

        internal readonly ArraySegment<byte> Complete;
        internal ArraySegment<byte> Message => Complete[..^HashSize];
        internal ArraySegment<byte> Hmac => Complete[^HashSize..];
    }
}