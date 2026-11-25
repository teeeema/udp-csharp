using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
namespace PredictionDemo
{
    public class MeteredTransport : UnityTransport
    {
        internal Func<Security.ISecureDatagramSocket> DatagramFactory { get; set; }
        public long SentBytes, ReceivedBytes;
        public override void Initialize(NetworkManager manager = null)
        {
            // Install on every initialization, including Shutdown/Restart. Fail closed:
            // no plaintext fallback or command-line downgrade in the current build.
            if (!(s_DriverConstructor is PredictionDemo.Security.ISecureDriverConstructor))
                s_DriverConstructor = new PredictionDemo.Security.SecureDriverConstructor();
            OnTransportEvent -= Count;
            base.Initialize(manager);
            SentBytes = ReceivedBytes = 0;
            OnTransportEvent += Count;
        }
        public override void Shutdown()
        {
            OnTransportEvent -= Count;
            base.Shutdown();
        }
        private void Count(NetworkEvent e, ulong id, ArraySegment<byte> data, float time)
        {
            if (e == NetworkEvent.Data) ReceivedBytes += data.Count;
        }
        public override void Send(ulong id, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            base.Send(id, data, delivery);
            SentBytes += data.Count;
        }
    }
}
