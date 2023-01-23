using System.Security.Cryptography;
using System.Text;
using static System.Security.Cryptography.HMACSHA512;

namespace WebStunnel;

internal enum CodecState {
    Init,
    Handshake,
    Active,
    Error
}

internal class Codec {
    private const int HashSize = 512 / 8;
    private readonly ArraySegment<byte> _key, _auth, _verify, _tmp;
    private readonly byte _protoByte;

    internal Codec(ProtocolByte protoByte, Config config) {
        if (string.IsNullOrEmpty(config.Key))
            throw new Exception("key required");

        _protoByte = (byte)protoByte;

        _key = SHA512.HashData(Encoding.UTF8.GetBytes(config.Key));

        _auth = new byte[HashSize];
        _verify = new byte[HashSize];
        _tmp = new byte[HashSize];

        State = CodecState.Init;
    }

    internal CodecState State { get; private set; }
    internal static int InitMessageSize => HashSize;

    internal ArraySegment<byte> InitHandshake(ArraySegment<byte> seg) {
        try {
            Transition(CodecState.Init, CodecState.Handshake);

            using var rng = RandomNumberGenerator.Create();

            seg = seg[..InitMessageSize];
            rng.GetBytes(seg);

            return seg;
        } catch {
            SetError();
            throw;
        }
    }

    internal void VerifyHandshake(ArraySegment<byte> seg) {
        try {
            Transition(CodecState.Handshake, CodecState.Active);

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

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg) {
        try {
            CheckState(CodecState.Active);
            return AuthMsg(seg);
        } catch {
            SetError();
            throw;
        }
    }

    internal ArraySegment<byte> VerifyMessage(ArraySegment<byte> seg) {
        try {
            CheckState(CodecState.Active);
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

    private void CheckState(CodecState expected) {
        if (State != expected)
            throw new Exception("invalid codec state");
    }

    private void Transition(CodecState expected, CodecState next) {
        CheckState(expected);
        State = next;
    }

    private void SetError() {
        State = CodecState.Error;
    }

    private static byte[] Cat(byte b, ArraySegment<byte> seg0, ArraySegment<byte> seg1) {
        var cat = new byte[1 + seg0.Count + seg1.Count];
        cat[0] = b;
        seg0.CopyTo(cat, 1);
        seg1.CopyTo(cat, seg0.Count + 1);
        return cat;
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