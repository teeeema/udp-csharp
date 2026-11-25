using System;
using System.Collections.Generic;

namespace PredictionDemo.Security
{
    public sealed class ReplayWindow
    {
        private ulong highest, bits;
        public bool Allows(ulong counter) => counter != 0 && (counter > highest ||
            (highest - counter < 64 && (bits & (1UL << (int)(highest - counter))) == 0));
        public void Accept(ulong counter)
        {
            if (!Allows(counter)) throw new InvalidOperationException("Replay");
            if (counter > highest)
            {
                ulong shift = counter - highest;
                bits = shift >= 64 ? 1 : (bits << (int)shift) | 1; highest = counter;
            }
            else bits |= 1UL << (int)(highest - counter);
        }
    }

    // All integers in the outer envelope are big endian. The complete original UTP
    // datagram (including its original headers) is carried verbatim inside the AEAD.
    public static class SecureWire
    {
        public const int HeaderSize = 20, Overhead = HeaderSize + 12 + 16;
        public const int MaxDatagram = 1200, MaxPlaintext = MaxDatagram - Overhead;
        public const byte KeyExchange = 10, Encrypted = 11, Data = 0, Hello = 1, Response = 2, Confirm = 1, Ready = 2;
        public static byte[] Slice(byte[] b, int start, int length)
        { var r = new byte[length]; Buffer.BlockCopy(b, start, r, 0, length); return r; }
        public static byte[] Join(byte[] a, byte[] b)
        { var r = new byte[a.Length + b.Length]; Buffer.BlockCopy(a, 0, r, 0, a.Length); Buffer.BlockCopy(b, 0, r, a.Length, b.Length); return r; }
        public static void Put64(byte[] b, int offset, ulong n)
        { for (int i = 7; i >= 0; i--) { b[offset + i] = (byte)n; n >>= 8; } }
        public static ulong Get64(byte[] b, int offset)
        { ulong n = 0; for (int i = 0; i < 8; i++) n = (n << 8) | b[offset + i]; return n; }
        public static byte[] Header(byte type, byte kind, bool fromServer, ulong id, int payloadSize)
        {
            var b = new byte[HeaderSize]; b[0] = 68; b[1] = 83; b[2] = 76; b[3] = 54;
            b[4] = 1; b[5] = type; b[6] = kind; b[7] = (byte)(fromServer ? 2 : 1);
            Put64(b, 8, id); b[16] = (byte)(payloadSize >> 8); b[17] = (byte)payloadSize;
            return b;
        }
        public static bool Valid(byte[] b) => b != null && b.Length >= HeaderSize && b.Length <= MaxDatagram &&
            b[0] == 68 && b[1] == 83 && b[2] == 76 && b[3] == 54 && b[4] == 1 &&
            (b[5] == KeyExchange || b[5] == Encrypted) && b[6] <= 2 && (b[7] == 1 || b[7] == 2) &&
            Get64(b, 8) != 0 && b[18] == 0 && b[19] == 0 && ((b[16] << 8) | b[17]) == b.Length - HeaderSize;
    }

    public sealed class SecurePeer : IDisposable
    {
        public enum Phase { AwaitResponse, AwaitConfirm, AwaitReady, Established }
        public Phase State;
        public readonly bool Server;
        public readonly ulong Id;
        public readonly double Created;
        public double LastAuthenticated, LastHandshake;
        public byte[] HelloPacket, ResponsePacket;
        private byte[] privateKey, sendKey, receiveKey, transcript;
        private ulong outgoing;
        private readonly ReplayWindow replay = new ReplayWindow();
        public SecurePeer(bool server, ulong id, double now)
        { Server = server; Id = id; Created = LastAuthenticated = now; privateKey = SecureCrypto.RandomBytes(32); }
        public byte[] Begin()
        {
            State = Phase.AwaitResponse;
            HelloPacket = SecureWire.Join(SecureWire.Header(10, 1, false, Id, 64),
                SecureWire.Join(SecureCrypto.PublicKey(privateKey), SecureCrypto.RandomBytes(32)));
            return HelloPacket;
        }
        public byte[] Respond(byte[] hello)
        {
            HelloPacket = (byte[])hello.Clone(); State = Phase.AwaitConfirm;
            ResponsePacket = SecureWire.Join(SecureWire.Header(10, 2, true, Id, 64),
                SecureWire.Join(SecureCrypto.PublicKey(privateKey), SecureWire.Slice(hello, 52, 32)));
            Derive(SecureWire.Slice(hello, 20, 32)); return ResponsePacket;
        }
        public bool ReceiveResponse(byte[] packet)
        {
            if (Server || State != Phase.AwaitResponse || !SecureWire.Valid(packet) || packet.Length != 84 ||
                packet[5] != 10 || packet[6] != 2 || packet[7] != 2 || SecureWire.Get64(packet, 8) != Id ||
                !SecureCrypto.Equal(SecureWire.Slice(packet, 52, 32), SecureWire.Slice(HelloPacket, 52, 32))) return false;
            ResponsePacket = (byte[])packet.Clone(); Derive(SecureWire.Slice(packet, 20, 32));
            State = Phase.AwaitReady; return true;
        }
        private void Derive(byte[] remotePublic)
        {
            byte[] secret = null;
            try
            {
                secret = SecureCrypto.Agreement(privateKey, remotePublic);
                transcript = SecureCrypto.Hash(SecureWire.Join(HelloPacket, ResponsePacket));
                sendKey = SecureCrypto.DirectionKey(secret, transcript, Server ? "s2c" : "c2s");
                receiveKey = SecureCrypto.DirectionKey(secret, transcript, Server ? "c2s" : "s2c");
            }
            finally { SecureCrypto.Wipe(secret); SecureCrypto.Wipe(privateKey); privateKey = null; }
        }
        public byte[] Seal(byte kind, byte[] plaintext)
        {
            if (sendKey == null || (kind == 0 && State != Phase.Established)) return null;
            if (plaintext == null || plaintext.Length > SecureWire.MaxPlaintext) throw new ArgumentException("Datagram too large");
            if (outgoing == ulong.MaxValue) throw new InvalidOperationException("Rekey required");
            var nonce = new byte[12]; nonce[3] = (byte)(Server ? 2 : 1); SecureWire.Put64(nonce, 4, ++outgoing);
            // payloadSize is FINAL before using the immutable header as AAD.
            var header = SecureWire.Header(11, kind, Server, Id, plaintext.Length + 28);
            return SecureWire.Join(SecureWire.Join(header, nonce), SecureCrypto.EncryptAES(sendKey, nonce, header, plaintext));
        }
        public byte[] Confirmation() => Seal(SecureWire.Confirm, transcript);
        public byte[] ReadyPacket() => Seal(SecureWire.Ready, transcript);
        public bool Open(byte[] packet, out byte[] plaintext, out string error)
        {
            plaintext = null; error = "AUTH_FAIL";
            if (receiveKey == null || !SecureWire.Valid(packet) || packet[5] != 11 || packet.Length < SecureWire.Overhead ||
                SecureWire.Get64(packet, 8) != Id || packet[7] != (Server ? 1 : 2) ||
                packet[20] != 0 || packet[21] != 0 || packet[22] != 0 || packet[23] != (Server ? 1 : 2)) return false;
            ulong counter = SecureWire.Get64(packet, 24);
            if (!replay.Allows(counter)) { error = "REPLAY_DROP"; return false; }
            if (!SecureCrypto.DecryptAES(receiveKey, SecureWire.Slice(packet, 20, 12), SecureWire.Slice(packet, 0, 20),
                SecureWire.Slice(packet, 32, packet.Length - 32), out plaintext)) return false;
            replay.Accept(counter); return true;
        }
        public bool CheckConfirmation(byte[] plaintext) => SecureCrypto.Equal(plaintext, transcript);
        public void Dispose()
        {
            SecureCrypto.Wipe(privateKey); SecureCrypto.Wipe(sendKey); SecureCrypto.Wipe(receiveKey); SecureCrypto.Wipe(transcript);
            privateKey = sendKey = receiveKey = transcript = null;
        }
    }

    public sealed class TokenBucket
    {
        private readonly double rate, burst;
        private double tokens, time;
        public TokenBucket(double rate, double burst, double now) { this.rate = rate; this.burst = burst; tokens = burst; time = now; }
        public bool Take(double now)
        { tokens = Math.Min(burst, tokens + Math.Max(0, now - time) * rate); time = now; if (tokens < 1) return false; tokens--; return true; }
    }

    // The engine owns no socket and uses a monotonic clock supplied by the adapter/tests.
    // All entry points are serialized by the managed UTP interface.
    public sealed class SecureSessionEngine : IDisposable
    {
        public const int MaxPending = 64, MaxSessions = 256, MaxRateSources = 1024;
        public const double HandshakeTimeout = 8, IdleTimeout = 30, RetryInterval = .5;
        private readonly bool server;
        private readonly Action<string, byte[]> send;
        private readonly Action<string> log;
        private readonly Dictionary<string, SecurePeer> peers = new Dictionary<string, SecurePeer>();
        // A reconnect from a reused UDP endpoint may negotiate a candidate, but an
        // unauthenticated Hello must never discard its existing established context.
        private readonly Dictionary<string, SecurePeer> replacements = new Dictionary<string, SecurePeer>();
        private sealed class Source { public TokenBucket Bucket; public double Last; }
        private readonly Dictionary<string, Source> sources = new Dictionary<string, Source>();
        private readonly Dictionary<string, double> logTimes = new Dictionary<string, double>();
        private readonly TokenBucket handshakeBudget, receiveBudget;
        private string target;
        public int SessionCount => peers.Count + replacements.Count;
        public long EncryptedPackets, PlaintextBytes, WireBytes;
        public SecureSessionEngine(bool server, Action<string, byte[]> send, Action<string> log = null)
        { this.server = server; this.send = send; this.log = log; handshakeBudget = new TokenBucket(20, 40, 0); receiveBudget = new TokenBucket(5000, 10000, 0); }
        private void Event(string code, double now)
        {
            if (!logTimes.TryGetValue(code, out var last) || now - last >= 1)
            { logTimes[code] = now; log?.Invoke("[PR6] " + code); }
        }
        public bool Established(string endpoint) => peers.TryGetValue(endpoint, out var p) && p.State == SecurePeer.Phase.Established;
        public bool SendGame(string endpoint, byte[] data, double now)
        {
            if (data == null || data.Length == 0 || data.Length > SecureWire.MaxPlaintext) return false;
            if (!peers.TryGetValue(endpoint, out var p))
            {
                if (server || (target != null && target != endpoint)) return false;
                target = endpoint; ulong id; do { id = SecureWire.Get64(SecureCrypto.RandomBytes(8), 0); } while (id == 0);
                p = new SecurePeer(false, id, now); peers[endpoint] = p; SendHandshake(endpoint, p.Begin(), p, now); return false;
            }
            if (p.State != SecurePeer.Phase.Established) return false;
            var packet = p.Seal(0, data); send(endpoint, packet);
            EncryptedPackets++; PlaintextBytes += data.Length; WireBytes += packet.Length; return true;
        }
        private void SendHandshake(string endpoint, byte[] packet, SecurePeer p, double now)
        { p.LastHandshake = now; send(endpoint, packet); }
        public byte[] Receive(string endpoint, string sourceIp, byte[] packet, double now)
        {
            if (!receiveBudget.Take(now)) { Event("RATE_LIMIT", now); return null; }
            if (!SecureWire.Valid(packet)) return null;
            if (!server && endpoint != target) return null;
            peers.TryGetValue(endpoint, out var p);
            if (packet[5] == 10)
            {
                if (server && packet[6] == 1 && packet[7] == 1 && packet.Length == 84)
                {
                    // Rate-limit retransmitted hellos too. No established context can be replaced by plaintext.
                    if (!AllowHandshake(sourceIp, now)) return null;
                    if (p != null)
                    {
                        if (SecureWire.Get64(packet, 8) == p.Id || p.State != SecurePeer.Phase.Established)
                        {
                            if (p.State != SecurePeer.Phase.Established && SecureCrypto.Equal(p.HelloPacket, packet))
                                SendHandshake(endpoint, p.ResponsePacket, p, now);
                            return null;
                        }
                    }
                    if (replacements.TryGetValue(endpoint, out var candidate))
                    {
                        if (SecureCrypto.Equal(candidate.HelloPacket, packet)) SendHandshake(endpoint, candidate.ResponsePacket, candidate, now);
                        return null;
                    }
                    int pending = replacements.Count; foreach (var peer in peers.Values) if (peer.State != SecurePeer.Phase.Established) pending++;
                    if (pending >= MaxPending || SessionCount >= MaxSessions) { Event("RATE_LIMIT", now); return null; }
                    var fresh = new SecurePeer(true, SecureWire.Get64(packet, 8), now);
                    try
                    {
                        var response = fresh.Respond(packet);
                        if (p == null) peers.Add(endpoint, fresh); else replacements.Add(endpoint, fresh);
                        SendHandshake(endpoint, response, fresh, now);
                    }
                    catch (ArgumentException) { fresh.Dispose(); Event("HANDSHAKE_FAIL", now); }
                }
                else if (!server && p != null && p.State == SecurePeer.Phase.AwaitResponse)
                {
                    try
                    {
                        if (p.ReceiveResponse(packet)) SendHandshake(endpoint, p.Confirmation(), p, now);
                        else Event("HANDSHAKE_FAIL", now);
                    }
                    catch (ArgumentException) { Remove(endpoint); Event("HANDSHAKE_FAIL", now); }
                }
                return null;
            }
            bool replacing = false;
            if (p == null || p.Id != SecureWire.Get64(packet, 8))
            {
                if (!replacements.TryGetValue(endpoint, out p) || p.Id != SecureWire.Get64(packet, 8)) return null;
                replacing = true;
            }
            if (!p.Open(packet, out var plaintext, out var error)) { Event(error, now); return null; }
            try
            {
                byte kind = packet[6];
                if (kind == 1 && server && p.CheckConfirmation(plaintext))
                {
                    if (replacing)
                    {
                        Remove(endpoint); peers[endpoint] = p; replacements.Remove(endpoint);
                    }
                    bool first = p.State != SecurePeer.Phase.Established; p.State = SecurePeer.Phase.Established;
                    p.LastAuthenticated = now; SendHandshake(endpoint, p.ReadyPacket(), p, now);
                    if (first) Event("HANDSHAKE_OK", now);
                }
                else if (kind == 2 && !server && p.State == SecurePeer.Phase.AwaitReady && p.CheckConfirmation(plaintext))
                { p.State = SecurePeer.Phase.Established; p.LastAuthenticated = now; Event("HANDSHAKE_OK", now); }
                else if (kind == 0 && p.State == SecurePeer.Phase.Established)
                { p.LastAuthenticated = now; var result = plaintext; plaintext = null; return result; }
                return null;
            }
            finally { SecureCrypto.Wipe(plaintext); }
        }
        private bool AllowHandshake(string ip, double now)
        {
            if (!handshakeBudget.Take(now)) { Event("RATE_LIMIT", now); return false; }
            if (!sources.TryGetValue(ip, out var source))
            {
                if (sources.Count >= MaxRateSources) { Event("RATE_LIMIT", now); return false; }
                source = new Source { Bucket = new TokenBucket(2, 4, now) }; sources.Add(ip, source);
            }
            source.Last = now;
            if (!source.Bucket.Take(now)) { Event("RATE_LIMIT", now); return false; }
            return true;
        }
        public void Tick(double now)
        {
            var expired = new List<string>();
            foreach (var entry in peers)
            {
                var p = entry.Value;
                if (p.State == SecurePeer.Phase.Established)
                { if (now - p.LastAuthenticated >= IdleTimeout) { expired.Add(entry.Key); Event("SESSION_EXPIRED", now); } }
                else if (now - p.Created >= HandshakeTimeout) { expired.Add(entry.Key); Event("HANDSHAKE_FAIL", now); }
                else if (now - p.LastHandshake >= RetryInterval)
                {
                    byte[] packet = p.State == SecurePeer.Phase.AwaitResponse ? p.HelloPacket :
                        p.State == SecurePeer.Phase.AwaitConfirm ? p.ResponsePacket : p.Confirmation();
                    SendHandshake(entry.Key, packet, p, now);
                }
            }
            foreach (var key in expired) Remove(key);
            expired.Clear();
            foreach (var entry in replacements)
            {
                var p = entry.Value;
                if (now - p.Created >= HandshakeTimeout) { expired.Add(entry.Key); Event("HANDSHAKE_FAIL", now); }
                else if (now - p.LastHandshake >= RetryInterval) SendHandshake(entry.Key, p.ResponsePacket, p, now);
            }
            foreach (var key in expired) { replacements[key].Dispose(); replacements.Remove(key); }
            expired.Clear(); foreach (var entry in sources) if (now - entry.Value.Last > 60) expired.Add(entry.Key);
            foreach (var key in expired) sources.Remove(key);
        }
        private void Remove(string endpoint) { if (peers.TryGetValue(endpoint, out var p)) { p.Dispose(); peers.Remove(endpoint); } }
        public void Dispose()
        {
            foreach (var p in peers.Values) p.Dispose(); foreach (var p in replacements.Values) p.Dispose();
            peers.Clear(); replacements.Clear(); sources.Clear(); target = null;
        }
    }
}
