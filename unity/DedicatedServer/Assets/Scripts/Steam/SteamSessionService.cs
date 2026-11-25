using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using PredictionDemo.Security;
using Steamworks;
using UnityEngine;

namespace PredictionDemo.Steam
{
    internal sealed class SteamSessionService : ISteamSessionService
    {
        private const string ServerVersion = DedicatedServerSettings.BuildId + ".0";
        private static readonly HServerListRequest InvalidRequest = HServerListRequest.Invalid;
        // Steamworks.NET 2025.165.0 roots native response objects. Reuse one response object
        // across refreshes. Only the active request owns the callbacks.
        private static ISteamMatchmakingServerListResponse searchResponse;
        private static SteamSessionService searchOwner;
        private readonly Dictionary<IPEndPoint, SessionInfo> results = new();
        private DedicatedServerSettings server;
        private SteamDatagramSocket socket;
        private Callback<SteamServersConnected_t> connected;
        private Callback<SteamServerConnectFailure_t> failed;
        private Callback<SteamServersDisconnected_t> disconnected;
        private HServerListRequest request = InvalidRequest;
        private Action<IReadOnlyList<SessionInfo>, string> completion;
        private EMatchMakingServerResponse? searchFinished;
        private bool initialized;
        private bool loggingOn;
        private bool advertised;
        private double loginDeadline;
        private uint appId;
        private int playerCount = -1;

        public string Status { get; private set; } = "Steam API idle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => SteamSessionPlatform.Create = () => new SteamSessionService();

        public void InitializeClient()
        {
            if (initialized)
            {
                if (!SteamUser.BLoggedOn()) throw new InvalidOperationException("Steam client is offline");
                return;
            }
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--pr7-app-id");
            string value = index < 0 ? Environment.GetEnvironmentVariable("SteamAppId") :
                index + 1 < args.Length ? args[index + 1] : null;
            if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out appId) || appId == 0 ||
                (appId == 480 && Array.IndexOf(args, "--pr7-allow-app-id-480") < 0))
            {
                Status = "Steam App ID requires configuration";
                throw new InvalidOperationException(Status);
            }
            SetAppId(appId);
            try
            {
                var result = SteamAPI.InitEx(out var error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                    throw new InvalidOperationException($"Steam client initialization failed ({result}): {error}");
                initialized = true;
                if (SteamUtils.GetAppID().m_AppId != appId)
                    throw new InvalidOperationException("The Steam client initialized a different App ID");
                if (!SteamUser.BLoggedOn()) throw new InvalidOperationException("Steam client is offline");
                Status = $"Steam client ready  /  App ID {appId}";
                Debug.Log($"[PR7] STEAM_INIT_OK role=client appid={appId}");
            }
            catch (Exception e)
            {
                if (initialized) SteamAPI.Shutdown();
                initialized = false;
                Status = "Steam client unavailable";
                throw new InvalidOperationException($"{Status}: {e.Message}", e);
            }
        }

        public void InitializeServer(DedicatedServerSettings settings)
        {
            server = settings;
            appId = settings.AppId;
            SetAppId(appId);
            byte[] address = settings.ListenAddress.GetAddressBytes();
            uint listen = ((uint)address[0] << 24) | ((uint)address[1] << 16) | ((uint)address[2] << 8) | address[3];
            var result = GameServer.InitEx(listen, settings.Port, settings.QueryPort,
                EServerMode.eServerModeAuthentication, ServerVersion, out var error);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException($"Steam Game Server initialization failed ({result}): {error}");
            initialized = true;
            if (SteamGameServerUtils.GetAppID().m_AppId != appId)
                throw new InvalidOperationException("Steam Game Server initialized a different App ID");

            connected = Callback<SteamServersConnected_t>.CreateGameServer(_ =>
            {
                loggingOn = false;
                SteamGameServer.SetAdvertiseServerActive(true);
                advertised = true;
                Status = "Steam Game Server logged on and advertising";
                Debug.Log($"[PR7] STEAM_ADVERTISE_REQUESTED appid={appId}");
            });
            failed = Callback<SteamServerConnectFailure_t>.CreateGameServer(value =>
            {
                Status = $"Steam Game Server logon failed ({value.m_eResult})";
                Debug.LogWarning($"[PR7] STEAM_LOGON_FAILED result={value.m_eResult} retry={value.m_bStillRetrying}");
                loggingOn = value.m_bStillRetrying;
            });
            disconnected = Callback<SteamServersDisconnected_t>.CreateGameServer(value =>
            {
                advertised = false;
                Status = $"Steam Game Server disconnected ({value.m_eResult})";
                Debug.LogWarning($"[PR7] STEAM_BACKEND_DISCONNECTED result={value.m_eResult}");
            });
            SteamGameServer.SetProduct(appId.ToString(CultureInfo.InvariantCulture));
            SteamGameServer.SetGameDescription("DUSTLINE");
            SteamGameServer.SetModDir(DedicatedServerSettings.ProjectTag);
            SteamGameServer.SetDedicatedServer(true);
            SteamGameServer.SetServerName(settings.Name);
            SteamGameServer.SetMapName(DedicatedServerSettings.Map);
            SteamGameServer.SetMaxPlayerCount(settings.MaxPlayers);
            SteamGameServer.SetKeyValue("PROJECT_TAG", DedicatedServerSettings.ProjectTag);
            SteamGameServer.SetKeyValue("BUILD_ID", DedicatedServerSettings.BuildId);
            SteamGameServer.SetKeyValue("SERVER_NAME", settings.Name);
            SteamGameServer.SetKeyValue("MAP_NAME", DedicatedServerSettings.Map);
            SteamGameServer.SetKeyValue("GAME_MODE", DedicatedServerSettings.GameMode);
            SteamGameServer.SetKeyValue("MAX_PLAYERS", settings.MaxPlayers.ToString(CultureInfo.InvariantCulture));
            SteamGameServer.SetKeyValue("NETWORK_PROFILE", "steam");
            UpdatePlayers(0);
            Status = $"Steam Game Server ready  /  App ID {appId}";
            Debug.Log($"[PR7] STEAM_INIT_OK role=dedicated appid={appId} query={settings.QueryPort}");
        }

        public void Advertise()
        {
            SteamGameServer.LogOnAnonymous();
            loggingOn = true;
            loginDeadline = Time.realtimeSinceStartupAsDouble + 45;
            Status = "Logging on to Steam Game Server";
        }

        public void UpdatePlayers(int count)
        {
            if (!initialized || server == null || count == playerCount) return;
            playerCount = count;
            string players = count.ToString(CultureInfo.InvariantCulture);
            SteamGameServer.SetKeyValue("CURRENT_PLAYERS", players);
            SteamGameServer.SetGameTags($"{DedicatedServerSettings.SteamTags},players-{players}");
        }

        public void Find(Action<IReadOnlyList<SessionInfo>, string> completed)
        {
            if (searchOwner != null) throw new InvalidOperationException("A Steam request is already active");
            searchResponse ??= new ISteamMatchmakingServerListResponse(
                (handle, index) => searchOwner?.ServerResponded(handle, index),
                (_, _) => { },
                (handle, result) =>
                {
                    if (searchOwner != null && handle == searchOwner.request) searchOwner.searchFinished = result;
                });
            results.Clear();
            completion = completed;
            searchOwner = this;
            try
            {
                var filter = new MatchMakingKeyValuePair_t { m_szKey = "gametagsand", m_szValue = DedicatedServerSettings.SteamTags };
                request = SteamMatchmakingServers.RequestInternetServerList(new AppId_t(appId),
                    new[] { filter }, 1, searchResponse);
                if (request == InvalidRequest) throw new InvalidOperationException("Steam could not start server search");
            }
            catch
            {
                CancelSearch();
                throw;
            }
        }

        private void ServerResponded(HServerListRequest handle, int index)
        {
            if (handle != request || results.Count >= 100) return;
            try
            {
                var item = SteamMatchmakingServers.GetServerDetails(handle, index);
                if (item == null || !item.m_bHadSuccessfulResponse || item.m_bPassword || item.m_nAppID != appId ||
                    item.m_NetAdr.GetIP() == 0 || item.m_NetAdr.GetConnectionPort() == 0 ||
                    !DedicatedServerSettings.TryReadSteamPlayerCount(item.GetGameTags(), item.m_nPlayers, item.m_nMaxPlayers, out var players))
                    return;
                var endpoint = SteamDatagramSocket.MakeEndpoint(item.m_NetAdr.GetIP(), item.m_NetAdr.GetConnectionPort());
                results[endpoint] = new SessionInfo(item.GetServerName(), item.GetMap(), DedicatedServerSettings.GameMode,
                    endpoint, players, item.m_nMaxPlayers);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PR7] STEAM_LIST_ENTRY_REJECTED type={e.GetType().Name}");
            }
        }

        public void CancelSearch()
        {
            if (request != InvalidRequest)
            {
                var handle = request;
                request = InvalidRequest;
                SteamMatchmakingServers.CancelQuery(handle);
                SteamMatchmakingServers.ReleaseRequest(handle);
            }
            if (searchOwner == this) searchOwner = null;
            completion = null;
            searchFinished = null;
        }

        public void Pump()
        {
            if (!initialized) return;
            if (server != null)
            {
                GameServer.RunCallbacks();
                if (loggingOn && Time.realtimeSinceStartupAsDouble >= loginDeadline)
                {
                    loggingOn = false;
                    Status = "Steam Game Server logon unconfirmed after 45 seconds";
                    Debug.LogWarning("[PR7] STEAM_LOGON_TIMEOUT");
                }
                return;
            }

            SteamAPI.RunCallbacks();
            if (!searchFinished.HasValue) return;
            var response = searchFinished.Value;
            var callback = completion;
            var list = new List<SessionInfo>(results.Values);
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            CancelSearch();
            string failure = response == EMatchMakingServerResponse.eServerFailedToRespond ?
                "Steam server list did not respond" : null;
            callback?.Invoke(list, failure);
        }

        public ISecureDatagramSocket CreateSocket(IPEndPoint endpoint)
        {
            socket?.Dispose();
            socket = new SteamDatagramSocket(endpoint, server?.MaxPlayers ?? 1);
            return socket;
        }

        private static void SetAppId(uint id)
        {
            string value = id.ToString(CultureInfo.InvariantCulture);
            Environment.SetEnvironmentVariable("SteamAppId", value);
            Environment.SetEnvironmentVariable("SteamGameId", value);
        }

        public void Dispose()
        {
            if (!initialized) return;
            socket?.Dispose();
            socket = null;
            if (server == null)
            {
                CancelSearch();
                SteamAPI.Shutdown();
            }
            else
            {
                if (advertised) SteamGameServer.SetAdvertiseServerActive(false);
                SteamGameServer.LogOff();
                connected?.Dispose();
                failed?.Dispose();
                disconnected?.Dispose();
                GameServer.Shutdown();
                Debug.Log("[PR7] STEAM_DEREGISTER_REQUESTED");
            }
            initialized = false;
        }
    }
}
