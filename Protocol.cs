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

internal class Protocol {
    private readonly ProtocolByte protoByte;
    private readonly char[] keyChars;

    private byte[] init;
    private Codec enc, dec;

    internal Protocol(ProtocolByte protoByte, Config config) {
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
            RandomNumberGenerator.Fill(msg.Plaintext);
            RandomNumberGenerator.Fill(msg.InitSalt);
            MakeCipher(msg.InitSalt).Tag(msg.Plaintext, msg.Tag);

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

            MakeCipher(thatMsg.InitSalt).VerifyTag(thatMsg.Plaintext, thatMsg.Tag);

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

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg, uint id) {
        try {
            CheckState(CodecState.Active);

            var ext = seg.Extend(Message.Data.SuffixSize);

            var msg = new Message.Data(ext);
            msg.Id = id;

            enc.Encrypt(msg.Text, msg.Tag);

            return ext;
        } catch {
            SetError();
            throw;
        }
    }

    internal (ArraySegment<byte>, uint) VerifyMessage(ArraySegment<byte> seg) {
        try {
            CheckState(CodecState.Active);

            var msg = new Message.Data(seg);

            dec.Decrypt(msg.Text, msg.Tag);

            return (msg.Payload, msg.Id);
        } catch {
            SetError();
            throw;
        }
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

    private Codec MakeCipher(ReadOnlySpan<byte> salt) {
        return new Codec(keyChars, salt);
    }

    private Codec MakeCipher(ReadOnlySpan<byte> salt0, ReadOnlySpan<byte> salt1) {
        var cat = new byte[salt0.Length + salt1.Length];
        salt0.CopyTo(cat);
        salt1.CopyTo(cat.AsSpan()[salt0.Length..]);
        return MakeCipher(cat);
    }

    private (Codec, Codec) MakeCiphers(Message.Init w, Message.Init t) {
        var c0 = MakeCipher(w.WriterSalt, t.ReaderSalt);
        var c1 = MakeCipher(w.ReaderSalt, t.WriterSalt);
        return (c0, c1);
    }
}
