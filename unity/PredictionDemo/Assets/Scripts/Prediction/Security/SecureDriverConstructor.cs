using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;

namespace PredictionDemo.Security
{
    public interface ISecureDriverConstructor : INetworkStreamDriverConstructor { }
    public sealed class SecureDriverConstructor : ISecureDriverConstructor
    {
        public static NetworkDriver CreateSecureDriver(UnityTransport transport, NetworkSettings settings)
        {
            // IPv4+UDP (28) or IPv6+UDP (48) + outer datagram <= 1248, below IPv6 min MTU.
            // Keep existing reliability/fragmentation settings, reducing only the inner MTU.
            var config = settings.GetNetworkConfigParameters();
            config.maxMessageSize = SecureWire.MaxPlaintext;
            settings.AddRawParameterStruct(ref config);
            return NetworkDriver.Create(new SecureNetworkInterface(), settings);
        }
        public void CreateDriver(UnityTransport transport, out NetworkDriver driver, out NetworkPipeline u,
            out NetworkPipeline us, out NetworkPipeline r)
        {
            driver = CreateSecureDriver(transport, transport.GetDefaultNetworkSettings());
            transport.GetDefaultPipelineConfigurations(ref driver, out var a, out var b, out var c);
            u = driver.CreatePipeline(a); us = driver.CreatePipeline(b); r = driver.CreatePipeline(c);
        }
    }
}
