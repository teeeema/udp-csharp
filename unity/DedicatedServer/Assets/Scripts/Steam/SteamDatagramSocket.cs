using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using PredictionDemo.Security;
using Steamworks;
using UnityEngine;

namespace PredictionDemo.Steam
{
    // This carrier accepts the same encrypted datagrams as UDP. Steam messages use
    // unreliable/no-delay delivery so UTP retains ownership of ordering and retries.
    internal sealed class SteamDatagramSocket : ISecureDatagramSocket
    {
        private readonly IPEndPoint target;
        private readonly int incomingLimit;
        private readonly Dictionary<IPEndPoint, HSteamNetConnection> connections = new();
        private readonly Dictionary<HSteamNetConnection, IPEndPoint> endpoints = new();
        private readonly IntPtr[] messages = new IntPtr[1];
        private HSteamListenSocket listener = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup poll = HSteamNetPollGroup.Invalid;
        private IPEndPoint local;
        private Callback<SteamNetConnectionStatusChangedCallback_t> changed;
        private bool disposed;
        private bool IsServer => target == null;

        public SteamDatagramSocket(IPEndPoint target, int maxPlayers)
        {
            this.target = target;
            incomingLimit = maxPlayers + 4;
        }

        public IPEndPoint Bind(IPEndPoint requested)
        {
            local = requested;
            poll = IsServer ? SteamGameServerNetworkingSockets.CreatePollGroup() : SteamNetworkingSockets.CreatePollGroup();
            if (poll == HSteamNetPollGroup.Invalid) return null;
            changed = IsServer
                ? Callback<SteamNetConnectionStatusChangedCallback_t>.CreateGameServer(ConnectionChanged)
                : Callback<SteamNetConnectionStatusChangedCallback_t>.Create(ConnectionChanged);
            if (IsServer) return local;

            SteamNetworkingSockets.InitAuthentication();
            var address = MakeAddress(target);
            var connection = SteamNetworkingSockets.ConnectByIPAddress(ref address, 0, null);
            if (connection == HSteamNetConnection.Invalid) return null;
            if (!SteamNetworkingSockets.SetConnectionPollGroup(connection, poll))
            {
                Close(connection);
                return null;
            }
            Remember(connection, target);
            // Steam manages the underlying client socket. UTP only uses this value
            // as its local logical endpoint.
            return new IPEndPoint(requested.Address, requested.Port == 0 ? 1 : requested.Port);
        }

        public bool Listen()
        {
            if (!IsServer || local == null || poll == HSteamNetPollGroup.Invalid) return false;
            SteamGameServerNetworkingSockets.InitAuthentication();
            var address = MakeAddress(local);
            listener = SteamGameServerNetworkingSockets.CreateListenSocketIP(ref address, 0, null);
            return listener != HSteamListenSocket.Invalid;
        }

        private static SteamNetworkingIPAddr MakeAddress(IPEndPoint endpoint)
        {
            var address = new SteamNetworkingIPAddr();
            // IPv4-mapped zero (0.0.0.0) listens on IPv4. Other binds are exact.
            byte[] ip = endpoint.Address.GetAddressBytes();
            uint numeric = ((uint)ip[0] << 24) | ((uint)ip[1] << 16) | ((uint)ip[2] << 8) | ip[3];
            address.SetIPv4(numeric, (ushort)endpoint.Port);
            return address;
        }

        public static IPEndPoint MakeEndpoint(uint ip, ushort port) => new(
            new IPAddress(new[] { (byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip }), port);

        private void ConnectionChanged(SteamNetConnectionStatusChangedCallback_t update)
        {
            if (disposed) return;
            var connection = update.m_hConn;
            var info = update.m_info;
            if (IsServer && info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting &&
                info.m_hListenSocket == listener && !endpoints.ContainsKey(connection))
            {
                Accept(connection, info.m_addrRemote);
                return;
            }
            if (!endpoints.TryGetValue(connection, out var endpoint)) return;
            switch (info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    Debug.Log($"[PR7] STEAM_SOCKET_CONNECTED role={(IsServer ? "dedicated" : "client")} addressType=IPv4 " +
                        $"connection=direct-ip relayPop={info.m_idPOPRelay.m_SteamNetworkingPOPID}");
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    endpoints.Remove(connection);
                    connections.Remove(endpoint);
                    Debug.Log($"[PR7] STEAM_SOCKET_CLOSED state={info.m_eState} reason={info.m_eEndReason}");
                    Close(connection);
                    break;
            }
        }

        private void Accept(HSteamNetConnection connection, SteamNetworkingIPAddr remote)
        {
            if (endpoints.Count >= incomingLimit || !remote.IsIPv4() || remote.m_port == 0 ||
                SteamGameServerNetworkingSockets.AcceptConnection(connection) != EResult.k_EResultOK ||
                !SteamGameServerNetworkingSockets.SetConnectionPollGroup(connection, poll))
            {
                Close(connection);
                return;
            }
            var endpoint = MakeEndpoint(remote.GetIPv4(), remote.m_port);
            // Several native connections can share one UDP endpoint. Keep the real
            // IPv4 for PR6 rate limiting, with a distinct logical port per connection.
            while (connections.ContainsKey(endpoint))
                endpoint = new IPEndPoint(endpoint.Address, endpoint.Port == ushort.MaxValue ? 1 : endpoint.Port + 1);
            Remember(connection, endpoint);
        }

        private void Remember(HSteamNetConnection connection, IPEndPoint endpoint)
        {
            endpoints.Add(connection, endpoint);
            connections.Add(endpoint, connection);
        }

        public int Receive(byte[] buffer, out IPEndPoint sender)
        {
            sender = null;
            if (disposed || poll == HSteamNetPollGroup.Invalid) return -1;
            int count = IsServer ? SteamGameServerNetworkingSockets.ReceiveMessagesOnPollGroup(poll, messages, 1) :
                SteamNetworkingSockets.ReceiveMessagesOnPollGroup(poll, messages, 1);
            if (count != 1) return -1;
            try
            {
                var message = SteamNetworkingMessage_t.FromIntPtr(messages[0]);
                if (!endpoints.TryGetValue(message.m_conn, out sender) || message.m_cbSize <= 0)
                    return -1;
                if (message.m_cbSize <= buffer.Length)
                    Marshal.Copy(message.m_pData, buffer, 0, message.m_cbSize);
                return message.m_cbSize;
            }
            finally
            {
                SteamNetworkingMessage_t.Release(messages[0]);
                messages[0] = IntPtr.Zero;
            }
        }

        public unsafe void Send(IPEndPoint destination, byte[] payload)
        {
            if (disposed || !connections.TryGetValue(destination, out var connection)) return;
            const int flags = Constants.k_nSteamNetworkingSend_UnreliableNoDelay;
            fixed (byte* data = payload)
            {
                if (IsServer)
                    SteamGameServerNetworkingSockets.SendMessageToConnection(connection, (IntPtr)data, (uint)payload.Length, flags, out _);
                else
                    SteamNetworkingSockets.SendMessageToConnection(connection, (IntPtr)data, (uint)payload.Length, flags, out _);
            }
        }

        private void Close(HSteamNetConnection connection)
        {
            if (IsServer) SteamGameServerNetworkingSockets.CloseConnection(connection, 0, null, false);
            else SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            changed?.Dispose();
            foreach (var connection in endpoints.Keys) Close(connection);
            if (listener != HSteamListenSocket.Invalid)
                SteamGameServerNetworkingSockets.CloseListenSocket(listener);
            if (poll != HSteamNetPollGroup.Invalid)
            {
                if (IsServer) SteamGameServerNetworkingSockets.DestroyPollGroup(poll);
                else SteamNetworkingSockets.DestroyPollGroup(poll);
            }
            endpoints.Clear();
            connections.Clear();
        }
    }
}
