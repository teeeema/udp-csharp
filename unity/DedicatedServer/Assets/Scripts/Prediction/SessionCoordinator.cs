using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace PredictionDemo
{
    public enum SessionState { Idle, Searching, ResultsReady, Joining, InGame, Disconnecting, Error }

    public sealed class SessionInfo
    {
        public string Name { get; }
        public string Map { get; }
        public string Mode { get; }
        public IPEndPoint Endpoint { get; }
        public int Players { get; }
        public int MaxPlayers { get; }

        public SessionInfo(string name, string map, string mode, IPEndPoint endpoint, int players, int maxPlayers)
        {
            Name = name;
            Map = map;
            Mode = mode;
            Endpoint = endpoint;
            Players = players;
            MaxPlayers = maxPlayers;
        }
    }

    public interface ISteamSessionService : IDisposable
    {
        string Status { get; }
        void InitializeClient();
        void InitializeServer(DedicatedServerSettings settings);
        void Advertise();
        void UpdatePlayers(int count);
        void Find(Action<IReadOnlyList<SessionInfo>, string> completed);
        void CancelSearch();
        void Pump();
        Security.ISecureDatagramSocket CreateSocket(IPEndPoint server);
    }

    public static class SteamSessionPlatform
    {
        public static Func<ISteamSessionService> Create { get; set; }
    }

    internal sealed class SessionCoordinator : IDisposable
    {
        private static readonly byte[] SteamPayload = Encoding.ASCII.GetBytes(
            $"{DedicatedServerSettings.ProjectTag}/{DedicatedServerSettings.BuildId}/steam");
        private readonly NetworkManager manager;
        private ISteamSessionService steam;
        private MeteredTransport transport;
        private double deadline;
        private SessionState afterShutdown;
        private bool disposed;
        private bool previousApproval;
        private int operation;

        public SessionState State { get; private set; }
        public string Status { get; private set; } = "Ready";
        public string SteamStatus => steam?.Status ?? "Steam API idle";
        public IReadOnlyList<SessionInfo> Servers { get; private set; } = Array.Empty<SessionInfo>();
        public bool CanStart => !manager.IsListening && !manager.ShutdownInProgress &&
            (State == SessionState.Idle || State == SessionState.ResultsReady || State == SessionState.Error);

        public SessionCoordinator(NetworkManager manager)
        {
            this.manager = manager;
            manager.OnClientStopped += OnClientStopped;
            manager.OnTransportFailure += OnTransportFailure;
        }

        public void Search()
        {
            if (!CanStart) return;
            Servers = Array.Empty<SessionInfo>();
            State = SessionState.Searching;
            Status = "Searching Steam servers…";
            deadline = Time.realtimeSinceStartupAsDouble + 20;
            int request = ++operation;
            try
            {
                steam ??= SteamSessionPlatform.Create?.Invoke() ?? throw new InvalidOperationException("Steam is unavailable on this platform");
                steam.InitializeClient();
                steam.Find((results, error) =>
                {
                    if (disposed || request != operation || State != SessionState.Searching) return;
                    if (error != null) { Fail(error); return; }
                    Servers = results;
                    State = SessionState.ResultsReady;
                    Status = results.Count == 0 ? "No compatible Steam servers found" : $"Found {results.Count} compatible servers";
                    Debug.Log($"[PR7] STEAM_SEARCH_OK count={results.Count}");
                });
            }
            catch (Exception e) { Fail(e.Message); }
        }

        public async void JoinDirect(string host, ushort port)
        {
            if (!CanStart) return;
            BeginJoin("Resolving direct IP endpoint…");
            int request = ++operation;
            try
            {
                if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Enter a server address");
                if (!IPAddress.TryParse(host, out var address))
                {
                    var addresses = await Dns.GetHostAddressesAsync(host);
                    address = Array.Find(addresses, ip => ip.AddressFamily == AddressFamily.InterNetwork);
                }
                if (disposed || operation != request) return;
                if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                    throw new ArgumentException("The server must have an IPv4 address");
                StartClient(new IPEndPoint(address, port), false);
            }
            catch (Exception e)
            {
                if (!disposed && operation == request) Fail(e.Message);
            }
        }

        public void Join(SessionInfo server)
        {
            if (!CanStart || State != SessionState.ResultsReady || server == null) return;
            if (server.Players >= server.MaxPlayers) { Fail("Selected server is full"); return; }
            BeginJoin("Connecting through Steam Networking Sockets…");
            try { StartClient(server.Endpoint, true); }
            catch (Exception e) { Fail(e.Message); }
        }

#if UNITY_EDITOR || (!UNITY_SERVER && DEVELOPMENT_BUILD)
        internal void TestSteamEndpoint(string host, ushort port)
        {
            if (!CanStart) return;
            BeginJoin("Opening the Steam socket for transport verification…");
            try
            {
                if (!IPAddress.TryParse(host, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                    throw new ArgumentException("Steam socket verification requires a numeric IPv4 address");
                steam ??= SteamSessionPlatform.Create?.Invoke() ?? throw new InvalidOperationException("Steam is unavailable on this platform");
                steam.InitializeClient();
                StartClient(new IPEndPoint(address, port), true);
            }
            catch (Exception e) { Fail(e.Message); }
        }
#endif

        private void BeginJoin(string status)
        {
            State = SessionState.Joining;
            Status = status;
            deadline = Time.realtimeSinceStartupAsDouble + 25;
        }

        private void StartClient(IPEndPoint endpoint, bool useSteam)
        {
            transport = manager.NetworkConfig.NetworkTransport as MeteredTransport ??
                throw new InvalidOperationException("Secure game transport is unavailable");
            transport.DatagramFactory = useSteam ? () => steam.CreateSocket(endpoint) : null;
            previousApproval = manager.NetworkConfig.ConnectionApproval;
            if (useSteam) manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ConnectionData = useSteam ? SteamPayload : Array.Empty<byte>();
            transport.SetConnectionData(endpoint.Address.ToString(), (ushort)endpoint.Port);
            Status = useSteam ? "Steam game connection pending" : "Secure client connecting";
            Debug.Log($"[PR7] JOIN profile={(useSteam ? "steam" : "ip")} transport={(useSteam ? "SteamNetworkingSockets+secure-UTP" : "secure-UTP/UDP")} addressType=IPv4");
            if (!manager.StartClient()) Fail("Client startup failed");
        }

        public void Connected(ulong clientId)
        {
            if (State != SessionState.Joining || clientId != manager.LocalClientId) return;
            State = SessionState.InGame;
            Status = $"Connected: client {clientId}";
            Debug.Log($"[PR7] JOIN_OK client={clientId}");
        }

        public void Disconnected(ulong clientId)
        {
            if (clientId != manager.LocalClientId && clientId != NetworkManager.ServerClientId) return;
            if (manager.ShutdownInProgress) return;
            if (State == SessionState.Joining || State == SessionState.InGame)
                Shutdown(SessionState.Error, string.IsNullOrWhiteSpace(manager.DisconnectReason) ?
                    "Game connection closed or lost" : manager.DisconnectReason);
        }

        public void Leave()
        {
            if (State == SessionState.Searching)
            {
                ++operation;
                steam?.CancelSearch();
                State = SessionState.Idle;
                Status = "Search cancelled";
                return;
            }
            if (State == SessionState.Joining || State == SessionState.InGame || manager.IsHost)
                Shutdown(SessionState.Idle, "Disconnected");
        }

        private void OnClientStopped(bool wasHost)
        {
            ResetTransport();
            if (State == SessionState.Disconnecting) State = afterShutdown;
            else if (State == SessionState.Joining || State == SessionState.InGame)
            {
                State = SessionState.Idle;
                Status = "Disconnected";
            }
        }

        private void ResetTransport()
        {
            if (transport != null)
            {
                transport.DatagramFactory = null;
                manager.NetworkConfig.ConnectionApproval = previousApproval;
            }
            manager.NetworkConfig.ConnectionData = Array.Empty<byte>();
            transport = null;
        }

        private void OnTransportFailure()
        {
            if (State == SessionState.Joining || State == SessionState.InGame) Fail("Game transport failed");
        }

        public void Tick()
        {
            steam?.Pump();
            if (State == SessionState.Disconnecting && !manager.IsListening && !manager.ShutdownInProgress)
            {
                ResetTransport();
                State = afterShutdown;
            }
            if (Time.realtimeSinceStartupAsDouble < deadline) return;
            if (State == SessionState.Joining) Fail("Game connection timed out");
            if (State == SessionState.Searching)
            {
                ++operation;
                steam?.CancelSearch();
                Fail("Steam search timed out");
            }
        }

        private void Fail(string status)
        {
            Debug.LogWarning($"[PR7] SESSION_ERROR {status}");
            Shutdown(SessionState.Error, status);
        }

        private void Shutdown(SessionState destination, string status)
        {
            ++operation;
            Status = status;
            afterShutdown = destination;
            if (manager.IsListening || manager.ShutdownInProgress)
            {
                State = SessionState.Disconnecting;
                if (!manager.ShutdownInProgress) manager.Shutdown();
                return;
            }
            ResetTransport();
            State = destination;
        }

        public static bool AcceptsSteamBuild(byte[] data) => data != null && data.AsSpan().SequenceEqual(SteamPayload);

        public void Dispose()
        {
            disposed = true;
            manager.OnClientStopped -= OnClientStopped;
            manager.OnTransportFailure -= OnTransportFailure;
            steam?.Dispose();
        }
    }
}
