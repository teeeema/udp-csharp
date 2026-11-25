using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PredictionDemo.Security;

namespace PredictionDemo.Tests
{
    public class SecureTransportTests
    {
        private static byte[] Hex(string s) => Enumerable.Range(0, s.Length / 2).Select(i => Convert.ToByte(s.Substring(i * 2, 2), 16)).ToArray();
        private sealed class Pair : IDisposable
        {
            public readonly SecureSessionEngine Client, Server;
            public readonly List<(bool server, byte[] data)> Queue = new List<(bool, byte[])>();
            public readonly List<byte[]> Delivered = new List<byte[]>();
            public readonly List<string> Events = new List<string>();
            public double Now;
            public Pair()
            {
                Client = new SecureSessionEngine(false, (e, b) => Queue.Add((false, b)), Events.Add);
                Server = new SecureSessionEngine(true, (e, b) => Queue.Add((true, b)), Events.Add);
            }
            public void Start() { Assert.IsFalse(Client.SendGame("s", new byte[] { 42 }, Now)); }
            public void Drain()
            {
                int n = 0;
                while (Queue.Count > 0)
                {
                    Assert.Less(++n, 50); var p = Queue[0]; Queue.RemoveAt(0);
                    var result = p.server ? Client.Receive("s", "server-ip", p.data, Now) : Server.Receive("c", "client-ip", p.data, Now);
                    if (result != null) Delivered.Add(result);
                }
            }
            public void Connect() { Start(); Drain(); Assert.IsTrue(Client.Established("s")); Assert.IsTrue(Server.Established("c")); }
            public void Advance(double now) { Now = now; Client.Tick(now); Server.Tick(now); Drain(); }
            public void Dispose() { Client.Dispose(); Server.Dispose(); }
        }
        [Test] public void X25519Rfc7748Vector()
        {
            var a = Hex("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a");
            var b = Hex("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb");
            Assert.AreEqual(Hex("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a"), SecureCrypto.PublicKey(a));
            var shared = SecureCrypto.Agreement(a, SecureCrypto.PublicKey(b));
            Assert.AreEqual(Hex("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742"), shared);
            Assert.AreEqual(shared, SecureCrypto.Agreement(b, SecureCrypto.PublicKey(a)));
        }
        [Test] public void InvalidLowOrderPublicKeyRejected() => Assert.Throws<ArgumentException>(() => SecureCrypto.Agreement(new byte[32], new byte[32]));
        [Test] public void HkdfRfc5869Vector()
        {
            Assert.AreEqual(Hex("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865"),
                SecureCrypto.Hkdf(Enumerable.Repeat((byte)11, 22).ToArray(), Hex("000102030405060708090a0b0c"), Hex("f0f1f2f3f4f5f6f7f8f9"), 42));
        }
        [Test] public void Aes256GcmNistVector()
        {
            var encrypted = SecureCrypto.EncryptAES(new byte[32], new byte[12], new byte[0], new byte[16]);
            Assert.AreEqual(Hex("cea7403d4d606b6e074ec5d3baf39d18d0d1c8a799996bf0265b98b5d48ab919"), encrypted);
            Assert.IsTrue(SecureCrypto.DecryptAES(new byte[32], new byte[12], new byte[0], encrypted, out var plaintext));
            Assert.AreEqual(new byte[16], plaintext);
        }
        [TestCase("ciphertext")] [TestCase("tag")] [TestCase("aad")] [TestCase("nonce")] [TestCase("key")]
        public void TamperingReturnsNoPartialPlaintext(string field)
        {
            var key = SecureCrypto.RandomBytes(32); var nonce = SecureCrypto.RandomBytes(12); var aad = new byte[] { 1, 2 };
            var encrypted = SecureCrypto.EncryptAES(key, nonce, aad, new byte[64]);
            switch (field) { case "ciphertext": encrypted[0] ^= 1; break; case "tag": encrypted[encrypted.Length - 1] ^= 1; break;
                case "aad": aad[0] ^= 1; break; case "nonce": nonce[0] ^= 1; break; case "key": key[0] ^= 1; break; }
            Assert.IsFalse(SecureCrypto.DecryptAES(key, nonce, aad, encrypted, out var plaintext)); Assert.IsNull(plaintext);
        }
        [Test] public void DirectionAndTranscriptSeparateKeys()
        {
            var secret = new byte[32]; var transcript = new byte[32];
            Assert.AreNotEqual(SecureCrypto.DirectionKey(secret, transcript, "c2s"), SecureCrypto.DirectionKey(secret, transcript, "s2c"));
            var first = SecureCrypto.DirectionKey(secret, transcript, "c2s"); transcript[0] = 1;
            Assert.AreNotEqual(first, SecureCrypto.DirectionKey(secret, transcript, "c2s"));
        }
        [Test] public void FullHandshakeAndBothDirections()
        {
            using (var p = new Pair()) { p.Connect(); Assert.IsTrue(p.Client.SendGame("s", new byte[] { 1 }, 1));
                Assert.IsTrue(p.Server.SendGame("c", new byte[] { 2 }, 1)); p.Drain();
                Assert.AreEqual(2, p.Delivered.Count); Assert.AreEqual(new byte[] { 1 }, p.Delivered[0]); Assert.Contains("[PR6] HANDSHAKE_OK", p.Events); }
        }
        [Test] public void GamesCannotPassBeforeConfirmation()
        {
            using (var p = new Pair()) { p.Start(); Assert.IsFalse(p.Client.SendGame("s", new byte[] { 1 }, 0));
                Assert.IsFalse(p.Server.SendGame("unknown", new byte[] { 1 }, 0)); Assert.IsNull(p.Server.Receive("c", "ip", new byte[40], 0)); Assert.AreEqual(0, p.Delivered.Count); }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryHandshakeStageSurvivesLoss(int stage)
        {
            using (var p = new Pair())
            {
                p.Start();
                for (int i = 0; i < stage; i++) { var packet = p.Queue[0]; p.Queue.RemoveAt(0);
                    if (packet.server) p.Client.Receive("s", "ip", packet.data, 0); else p.Server.Receive("c", "ip", packet.data, 0); }
                p.Queue.Clear(); p.Advance(.6); p.Advance(1.2);
                Assert.IsTrue(p.Client.Established("s")); Assert.IsTrue(p.Server.Established("c"));
            }
        }
        [Test] public void UnfinishedHandshakeExpiresAndDisposes()
        { using (var p = new Pair()) { p.Start(); p.Queue.Clear(); p.Client.Tick(9); Assert.AreEqual(0, p.Client.SessionCount); Assert.Contains("[PR6] HANDSHAKE_FAIL", p.Events); } }
        [Test] public void IdleSessionExpires()
        { using (var p = new Pair()) { p.Connect(); p.Advance(31); Assert.AreEqual(0, p.Client.SessionCount); Assert.AreEqual(0, p.Server.SessionCount); Assert.Contains("[PR6] SESSION_EXPIRED", p.Events); } }
        [Test] public void ReconnectHasNewSessionId()
        { using (var p = new Pair()) { p.Start(); var id = SecureWire.Get64(p.Queue[0].data, 8); p.Queue.Clear(); p.Client.Tick(9); p.Now = 10; p.Start(); Assert.AreNotEqual(id, SecureWire.Get64(p.Queue[0].data, 8)); } }
        [Test] public void ReusedEndpointReplacesKeysOnlyAfterNewConfirmation()
        {
            using (var p = new Pair())
            {
                p.Connect(); p.Client.SendGame("s", new byte[] { 7 }, 1); var oldPacket = p.Queue[0].data; p.Queue.Clear();
                var outgoing = new List<byte[]>();
                using (var fresh = new SecureSessionEngine(false, (e, b) => outgoing.Add(b)))
                {
                    fresh.SendGame("s", new byte[] { 1 }, 1);
                    p.Server.Receive("c", "client-ip", outgoing[0], 1); outgoing.Clear();
                    Assert.AreEqual(2, p.Server.SessionCount); Assert.IsTrue(p.Server.Established("c"));
                    Assert.AreEqual(new byte[] { 7 }, p.Server.Receive("c", "client-ip", oldPacket, 1));
                    fresh.Receive("s", "server-ip", p.Queue[0].data, 1); p.Queue.Clear();
                    p.Server.Receive("c", "client-ip", outgoing[0], 1); outgoing.Clear();
                    fresh.Receive("s", "server-ip", p.Queue[0].data, 1); p.Queue.Clear();
                    Assert.IsTrue(fresh.Established("s")); Assert.AreEqual(1, p.Server.SessionCount);
                    Assert.IsNull(p.Server.Receive("c", "client-ip", oldPacket, 1));
                    fresh.SendGame("s", new byte[] { 8 }, 1);
                    Assert.AreEqual(new byte[] { 8 }, p.Server.Receive("c", "client-ip", outgoing[0], 1));
                }
            }
        }
        [Test] public void DuplicateHelloKeepsContext()
        { using (var p = new Pair()) { p.Start(); var hello = p.Queue[0].data; p.Server.Receive("c", "ip", hello, 0); p.Server.Receive("c", "ip", hello, .1); Assert.AreEqual(1, p.Server.SessionCount); p.Drain(); Assert.IsTrue(p.Client.Established("s")); } }
        [Test] public void PlaintextHelloCannotReplaceEstablishedSession()
        { using (var p = new Pair()) { p.Start(); var hello = p.Queue[0].data; p.Drain(); hello[8] ^= 1; p.Server.Receive("c", "ip", hello, 1); Assert.IsTrue(p.Client.SendGame("s", new byte[] { 1 }, 1)); p.Drain(); Assert.AreEqual(1, p.Delivered.Count); } }
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(16)] [TestCase(52)]
        public void WrongResponseRejected(int offset)
        { using (var p = new Pair()) { p.Start(); var hello = p.Queue[0].data; p.Queue.Clear(); p.Server.Receive("c", "ip", hello, 0); var response = (byte[])p.Queue[0].data.Clone(); response[offset] ^= 1; p.Queue.Clear(); p.Client.Receive("s", "ip", response, 0); Assert.IsFalse(p.Client.Established("s")); Assert.IsEmpty(p.Queue); } }
        [Test] public void WrongSenderCannotEstablishClient()
        { using (var p = new Pair()) { p.Start(); var hello = p.Queue[0].data; p.Queue.Clear(); p.Server.Receive("c", "ip", hello, 0); var response = p.Queue[0].data; p.Queue.Clear(); p.Client.Receive("attacker", "ip", response, 0); Assert.IsEmpty(p.Queue); } }
        [Test] public void ReplayIsDroppedAndReorderingAccepted()
        {
            using (var p = new Pair()) { p.Connect(); for (byte i = 0; i < 3; i++) p.Client.SendGame("s", new[] { i }, 1);
                var packets = p.Queue.Select(x => x.data).ToArray(); p.Queue.Clear();
                foreach (int i in new[] { 2, 0, 1 }) Assert.AreEqual(new[] { (byte)i }, p.Server.Receive("c", "ip", packets[i], 1));
                Assert.IsNull(p.Server.Receive("c", "ip", packets[0], 1)); Assert.Contains("[PR6] REPLAY_DROP", p.Events); }
        }
        [Test] public void OldWindowPacketDropped()
        { var w = new ReplayWindow(); Assert.IsFalse(w.Allows(0)); w.Accept(100); Assert.IsTrue(w.Allows(37)); Assert.IsFalse(w.Allows(36)); w.Accept(37); Assert.IsFalse(w.Allows(37)); w.Accept(200); Assert.IsFalse(w.Allows(100)); }
        [Test] public void ForgedHighCounterDoesNotPoisonReplayWindow()
        { using (var p = new Pair()) { p.Connect(); p.Client.SendGame("s", new byte[] { 1 }, 1); var valid = p.Queue[0].data; var bad = (byte[])valid.Clone(); SecureWire.Put64(bad, 24, ulong.MaxValue); Assert.IsNull(p.Server.Receive("c", "ip", bad, 1)); Assert.IsNotNull(p.Server.Receive("c", "ip", valid, 1)); } }
        public static IEnumerable<int> HeaderOffsets => Enumerable.Range(0, SecureWire.HeaderSize);
        [TestCaseSource(nameof(HeaderOffsets))] public void EveryHeaderByteAuthenticatedOrRejected(int offset)
        { using (var p = new Pair()) { p.Connect(); p.Client.SendGame("s", new byte[] { 1, 2, 3 }, 1); var valid = p.Queue[0].data; var bad = (byte[])valid.Clone(); bad[offset] ^= 1; Assert.IsNull(p.Server.Receive("c", "ip", bad, 1)); Assert.IsNotNull(p.Server.Receive("c", "ip", valid, 1)); } }
        [Test] public void EmptyTruncatedOversizedAndRandomPacketsDoNotCrash()
        {
            using (var p = new Pair()) { p.Connect(); p.Client.SendGame("s", new byte[10], 1); var valid = p.Queue[0].data;
                Assert.IsNull(p.Server.Receive("c", "ip", null, 1));
                for (int n = 0; n < valid.Length; n++) Assert.IsNull(p.Server.Receive("c", "ip", SecureWire.Slice(valid, 0, n), 1));
                for (int n = 0; n < 1300; n++) Assert.IsNull(p.Server.Receive("unknown", "ip", SecureCrypto.RandomBytes(n), 1));
                Assert.IsNull(p.Server.Receive("c", "ip", new byte[1201], 1)); Assert.IsFalse(p.Client.SendGame("s", new byte[1153], 1)); }
        }
        [Test] public void MaxSizeAndFinalPayloadSize()
        { using (var p = new Pair()) { p.Connect(); p.Client.SendGame("s", new byte[SecureWire.MaxPlaintext], 1); var packet = p.Queue[0].data; Assert.AreEqual(1200, packet.Length); Assert.IsTrue(SecureWire.Valid(packet)); p.Drain(); Assert.AreEqual(1152, p.Delivered[0].Length); } }
        [Test] public void RetransmissionGetsFreshNonceAndSameGameBytes()
        { using (var p = new Pair()) { p.Connect(); p.Client.SendGame("s", new byte[] { 4 }, 1); p.Client.SendGame("s", new byte[] { 4 }, 1);
            Assert.AreNotEqual(SecureWire.Get64(p.Queue[0].data, 24), SecureWire.Get64(p.Queue[1].data, 24)); p.Drain(); Assert.AreEqual(p.Delivered[0], p.Delivered[1]); } }
        [Test] public void IndependentClientsCannotDecryptEachOther()
        { using (var a = new Pair()) using (var b = new Pair()) { a.Connect(); b.Connect(); a.Client.SendGame("s", new byte[] { 1 }, 1); Assert.IsNull(b.Server.Receive("c", "ip", a.Queue[0].data, 1)); } }
        [Test] public void SourceLimitIncludesPortsAndCapsPendingSessions()
        {
            var events = new List<string>();
            using (var server = new SecureSessionEngine(true, (e, b) => { }, events.Add))
            {
                for (int i = 0; i < 100; i++) using (var peer = new SecurePeer(false, (ulong)i + 1, 0)) server.Receive("endpoint" + i, "same-ip", peer.Begin(), 0);
                Assert.AreEqual(4, server.SessionCount); Assert.Contains("[PR6] RATE_LIMIT", events);
                for (int i = 100; i < 1000; i++) using (var peer = new SecurePeer(false, (ulong)i + 1, 0)) server.Receive("endpoint" + i, "ip" + i, peer.Begin(), 1+(i-100)*.02);
                Assert.AreEqual(SecureSessionEngine.MaxPending, server.SessionCount); server.Tick(40); Assert.AreEqual(0, server.SessionCount);
            }
        }
        [Test] public void TokenBucketRefillsAndDoesNotMintTokensAtSameTime()
        { var b = new TokenBucket(2, 4, 0); for (int i = 0; i < 4; i++) Assert.IsTrue(b.Take(0)); Assert.IsFalse(b.Take(0)); Assert.IsTrue(b.Take(.5)); Assert.IsFalse(b.Take(.5)); }
        [Test] public void TotalEstablishedSessionCapacityIsBounded()
        {
            var queue = new List<(string endpoint, byte[] data, bool fromServer)>();
            var clients = new Dictionary<string, SecureSessionEngine>();
            using (var server = new SecureSessionEngine(true, (e, b) => queue.Add((e, b, true))))
            try
            {
                for (int i = 0; i <= SecureSessionEngine.MaxSessions; i++)
                {
                    string endpoint = "client" + i; double now = i * .06;
                    var client = new SecureSessionEngine(false, (e, b) => queue.Add((endpoint, b, false))); clients.Add(endpoint, client);
                    client.SendGame("server", new byte[] { 1 }, now);
                    while (queue.Count != 0)
                    {
                        var packet = queue[0]; queue.RemoveAt(0);
                        if (packet.fromServer) clients[packet.endpoint].Receive("server", "server-ip", packet.data, now);
                        else server.Receive(packet.endpoint, packet.endpoint, packet.data, now);
                    }
                    Assert.AreEqual(i < SecureSessionEngine.MaxSessions, client.Established("server"));
                }
                Assert.AreEqual(SecureSessionEngine.MaxSessions, server.SessionCount);
            }
            finally { foreach (var c in clients.Values) c.Dispose(); }
        }
        [Test] public void DisposeDisablesOldContextAndNonceCounterCannotWrap()
        {
            using (var client = new SecurePeer(false, 1, 0)) using (var server = new SecurePeer(true, 1, 0))
            {
                var hello = client.Begin(); var response = server.Respond(hello); Assert.IsTrue(client.ReceiveResponse(response));
                client.State = SecurePeer.Phase.Established;
                typeof(SecurePeer).GetField("outgoing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(client, ulong.MaxValue);
                Assert.Throws<InvalidOperationException>(() => client.Seal(0, new byte[] { 1 }));
                client.Dispose(); Assert.IsNull(client.Seal(0, new byte[] { 1 }));
            }
        }
        [Test] public void InvalidPublicKeyInRealHelloDoesNotAllocateSession()
        {
            var events = new List<string>();
            using (var peer = new SecurePeer(false, 1, 0)) using (var server = new SecureSessionEngine(true, (e, b) => Assert.Fail("No response for invalid key"), events.Add))
            { var hello = peer.Begin(); Array.Clear(hello, 20, 32); server.Receive("c", "ip", hello, 0); Assert.AreEqual(0, server.SessionCount); Assert.Contains("[PR6] HANDSHAKE_FAIL", events); }
        }
        [Test] public void PlaintextGameIsRejectedEvenAfterHandshake()
        { using (var p = new Pair()) { p.Connect(); Assert.IsNull(p.Server.Receive("c", "ip", Encoding.ASCII.GetBytes("MOVEMENT SHOOT"), 1)); } }
        [Test] public void LogsContainOnlySafeEventNames()
        { using (var p = new Pair()) { p.Connect(); foreach (var e in p.Events) StringAssert.IsMatch(@"^\[PR6\] (HANDSHAKE_OK|HANDSHAKE_FAIL|AUTH_FAIL|REPLAY_DROP|RATE_LIMIT|SESSION_EXPIRED)$", e); } }
    }
}
