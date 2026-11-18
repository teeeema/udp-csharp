using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Unity.Jobs;
using Unity.Networking.Transport;

namespace PredictionDemo.Security
{
    // Managed INetworkInterface is a supported UTP 6.6 extension point. Completing
    // dependencies here intentionally trades throughput for simple serialized ownership.
    // Encryption sits BELOW reliability, so UTP retransmissions get fresh AEAD counters.
    public sealed class SecureNetworkInterface : INetworkInterface
    {
        private Socket socket;
        private SecureSessionEngine engine;
        private NetworkEndpoint local;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly byte[] receiveBuffer = new byte[65536];
        private bool disposed;
        public NetworkEndpoint LocalEndpoint => local;
        public int Initialize(ref NetworkSettings settings, ref int packetPadding) => 0;
        public int Bind(NetworkEndpoint endpoint)
        {
            try
            {
                var ip = IPAddress.Parse(endpoint.ToFixedStringNoPort().ToString());
                socket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
                socket.ReceiveBufferSize = socket.SendBufferSize = 256 * 1024;
                socket.Bind(new IPEndPoint(ip, endpoint.Port));
                var bound = (IPEndPoint)socket.LocalEndPoint;
                local = NetworkEndpoint.Parse(bound.Address.ToString(), (ushort)bound.Port,
                    bound.AddressFamily == AddressFamily.InterNetworkV6 ? NetworkFamily.Ipv6 : NetworkFamily.Ipv4);
                engine = new SecureSessionEngine(false, SendWire, UnityEngine.Debug.Log);
                return 0;
            }
            catch (SocketException) { socket?.Dispose(); socket = null; return -1; }
        }
        public int Listen()
        {
            engine?.Dispose(); engine = new SecureSessionEngine(true, SendWire, UnityEngine.Debug.Log); return 0;
        }
        private static IPEndPoint ParseEndpoint(string endpoint)
        {
            int colon = endpoint.LastIndexOf(':');
            return new IPEndPoint(IPAddress.Parse(endpoint.Substring(0, colon).Trim('[', ']')), int.Parse(endpoint.Substring(colon + 1)));
        }
        private void SendWire(string endpoint, byte[] bytes)
        {
            try { socket.SendTo(bytes, ParseEndpoint(endpoint)); }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.NoBufferSpaceAvailable || e.SocketErrorCode == SocketError.ConnectionReset) { }
        }
        public unsafe JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            dependency.Complete();
            double now = clock.Elapsed.TotalSeconds; engine.Tick(now);
            // Bound work per update even under an unauthenticated UDP flood.
            for (int i = 0; i < 256; i++)
            {
                EndPoint source = socket.AddressFamily == AddressFamily.InterNetworkV6 ?
                    new IPEndPoint(IPAddress.IPv6Any, 0) : new IPEndPoint(IPAddress.Any, 0);
                int count;
                try { count = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref source); }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.ConnectionReset) { break; }
                if (count > SecureWire.MaxDatagram) continue;
                var remote = (IPEndPoint)source;
                var bytes = new byte[count]; Buffer.BlockCopy(receiveBuffer, 0, bytes, 0, count);
                var plaintext = engine.Receive(remote.ToString(), remote.Address.ToString(), bytes, now);
                if (plaintext == null) continue;
                try
                {
                    if (!arguments.ReceiveQueue.EnqueuePacket(out var packet)) break;
                    if (plaintext.Length > packet.BytesAvailableAtEnd) { packet.Drop(); continue; }
                    packet.EndpointRef = NetworkEndpoint.Parse(remote.Address.ToString(), (ushort)remote.Port,
                        remote.AddressFamily == AddressFamily.InterNetworkV6 ? NetworkFamily.Ipv6 : NetworkFamily.Ipv4);
                    fixed (byte* p = plaintext) packet.AppendToPayload(p, plaintext.Length);
                }
                finally { SecureCrypto.Wipe(plaintext); }
            }
            return default;
        }
        public unsafe JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            dependency.Complete();
            for (int i = 0; i < arguments.SendQueue.Count; i++)
            {
                var packet = arguments.SendQueue[i]; if (packet.Length <= 0) continue;
                var plaintext = new byte[packet.Length];
                try
                {
                    fixed (byte* p = plaintext) packet.CopyPayload(p, plaintext.Length);
                    engine.SendGame(packet.EndpointRef.ToString(), plaintext, clock.Elapsed.TotalSeconds);
                }
                finally { SecureCrypto.Wipe(plaintext); packet.Drop(); }
            }
            return default;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (engine != null)
                UnityEngine.Debug.Log($"[PR6] TRAFFIC packets={engine.EncryptedPackets} inner_bytes={engine.PlaintextBytes} wire_bytes={engine.WireBytes}");
            engine?.Dispose(); socket?.Dispose(); SecureCrypto.Wipe(receiveBuffer);
        }
    }
}
