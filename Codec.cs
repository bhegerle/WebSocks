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

    private readonly ArraySegment<byte> key, auth, verify, tmp;
    private readonly ProtocolByte protoByte;
    private readonly char[] keyChars;

    private byte[] init;
    private Cipher enc, dec;

    internal Codec(ProtocolByte protoByte, Config config) {
        if (string.IsNullOrEmpty(config.Key))
            throw new Exception("key required");

        this.protoByte = protoByte;

        keyChars = config.Key.ToArray();

        State = CodecState.Init;
    }

    internal CodecState State { get; private set; }
    internal static int InitMessageSize => Message.Init.MessageSize;

    internal ArraySegment<byte> InitHandshake() {
        try {
            init = new byte[Message.Init.MessageSize];

            var msg = new Message.Init(init);
            RandomNumberGenerator.Fill(msg.SaltPair.Buffer);
            RandomNumberGenerator.Fill(msg.Tagged.Salt);
            MakeCipher(msg.Tagged.Salt).Tag(msg.Tagged.Content, msg.Tagged.Tag);

            Transition(CodecState.Init, CodecState.Handshake);

            return init;
        } catch {
            SetError();
            throw;
        }
    }

    internal void VerifyHandshake(ArraySegment<byte> vseg) {
        try {
            if (vseg.Count != InitMessageSize)
                throw new Exception("wrong size for init handshake message");

            var thatMsg = new Message.Init(vseg);

            MakeCipher(thatMsg.Tagged.Salt).VerifyTag(thatMsg.Tagged.Content, thatMsg.Tagged.Tag);

            var thisMsg = new Message.Init(init);
            if (protoByte == ProtocolByte.WsListener)
                (enc, dec) = MakeCiphers(thisMsg, thatMsg);
            else
                (dec, enc) = MakeCiphers(thatMsg, thisMsg);

            Transition(CodecState.Handshake, CodecState.Active);
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
        var msg = new Frame(seg, HashSize, true);

        auth.CopyTo(msg.Suffix);
        HashData(key, msg.Complete, auth);

        auth.CopyTo(msg.Suffix);

        return msg.Complete;
    }

    private ArraySegment<byte> VerifyMsg(ArraySegment<byte> seg) {
        var msg = new Frame(seg, HashSize, false);

        msg.Suffix.CopyTo(tmp);
        verify.CopyTo(msg.Suffix);
        HashData(key, msg.Complete, verify);

        if (!Utils.ConjEqual(verify, tmp))
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

    private Cipher MakeCipher(ReadOnlySpan<byte> salt) {
        return new Cipher(keyChars, salt);
    }

    private Cipher MakeCipher(ReadOnlySpan<byte> salt0, ReadOnlySpan<byte> salt1) {
        var cat = new byte[salt0.Length + salt1.Length];
        salt0.CopyTo(cat);
        salt1.CopyTo(cat.AsSpan()[salt0.Length..]);
        return MakeCipher(cat);
    }

    private (Cipher, Cipher) MakeCiphers(Message.Init w, Message.Init t) {
        var c0 = MakeCipher(w.SaltPair.WriterSalt, t.SaltPair.ReaderSalt);
        var c1 = MakeCipher(w.SaltPair.ReaderSalt, t.SaltPair.WriterSalt);
        return (c0, c1);
    }
}
