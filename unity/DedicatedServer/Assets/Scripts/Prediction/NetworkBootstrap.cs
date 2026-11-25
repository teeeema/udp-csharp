using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PredictionDemo
{
    internal static class PredictionHudLayout
    {
        private const float ReferenceWidth = 960.0f;
        private const float ReferenceHeight = 540.0f;

        public static Matrix4x4 BeginScaledGui()
        {
            var previous = GUI.matrix;
            var widthScale = Mathf.Max(1, Screen.width) / ReferenceWidth;
            var heightScale = Mathf.Max(1, Screen.height) / ReferenceHeight;
            var scale = Mathf.Min(widthScale, heightScale);

            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1.0f));
            return previous;
        }

        public static void EndScaledGui(Matrix4x4 previous)
        {
            GUI.matrix = previous;
        }
    }

    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = 7777;

        private NetworkManager _networkManager;
        private UnityTransport _transport;
        private string _status = "Ready";
        private string _screenshotPath;
        private float _screenshotAtSeconds = -1.0f;
        private float _exitAfterSeconds = -1.0f;
        private int _waitForPlayers;
        private SessionCoordinator _sessions;
        private DedicatedServerSettings _dedicated;
        private ISteamSessionService _serverSteam;
        private readonly System.Collections.Generic.Dictionary<ulong, int> _spawnSlots = new();
        private string _stopFile;
        private double _nextStopCheck;
        private bool _stopping;

        private void Awake()
        {
            Application.runInBackground = true;
            _networkManager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            _networkManager.NetworkConfig.EnableSceneManagement = false;
            _networkManager.AddNetworkPrefab(playerPrefab);
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            _networkManager.OnServerStopped += OnServerStopped;
            _sessions = new SessionCoordinator(_networkManager);
        }

        private IEnumerator Start()
        {
            yield return null;

            var args = Environment.GetCommandLineArgs();
#if UNITY_SERVER && !UNITY_EDITOR
            StartDedicated(args);
            yield break;
#else
            if (Array.IndexOf(args, "--pr7-server") >= 0)
            {
                StartDedicated(args);
                yield break;
            }
            var startHost = false;
            var startClient = false;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--pr4-profile" && i + 1 < args.Length && byte.TryParse(args[i + 1], out var p))
                {
                    NetworkLabSettings.ActiveProfile = (LabProfile)Mathf.Clamp(p, 0, 2);
                    i++;
                }
                else if (args[i] == "--pr6-address" && i + 1 < args.Length)
                {
                    address = args[++i];
                }
                else if (args[i] == "--pr6-wait-players" && i + 1 < args.Length && int.TryParse(args[i + 1], out var playerCount))
                {
                    _waitForPlayers = Mathf.Clamp(playerCount, 2, 16); i++;
                    // Explicit verification only: keep multiple graphical players from
                    // rendering without a frame limit and starving each other's updates.
                    QualitySettings.vSyncCount = 0; Application.targetFrameRate = 30;
                }
                else if (args[i] == "--pr4-no-prediction")
                {
                    NetworkLabSettings.PredictionEnabled = false;
                }
                else if (args[i] == "--pr4-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out var demoPort) && demoPort > 0)
                {
                    port = demoPort;
                    i++;
                }
                else if (args[i] == "--pr4-auto-fire")
                {
                    NetworkLabSettings.AutomationFire = true;
                }
                else if (args[i] == "--pr4-loss" && i + 1 < args.Length && int.TryParse(args[i + 1], out var loss))
                {
                    NetworkLabSettings.CustomDelayMs = NetworkLabSettings.GetProfile(NetworkLabSettings.ActiveProfile).OneWayDelayMs;
                    NetworkLabSettings.CustomLossPercent = Mathf.Clamp(loss, 0, 100);
                    NetworkLabSettings.ActiveProfile = LabProfile.Custom;
                    i++;
                }
                else if (args[i] == "--pr4-auto")
                {
                    NetworkLabSettings.AutomationEnabled = true;
                }
                else if (args[i] == "--pr4-auto-divergence")
                {
                    NetworkLabSettings.AutomationEnabled = true;
                    NetworkLabSettings.AutomationDivergence = true;
                }
                else if (args[i] == "--pr4-screenshot" && i + 1 < args.Length)
                {
                    _screenshotPath = args[++i];
                }
                else if (args[i] == "--pr4-screenshot-at" && i + 1 < args.Length && TryParseFloat(args[i + 1], out var screenshotAt))
                {
                    _screenshotAtSeconds = Mathf.Max(0.1f, screenshotAt);
                    i++;
                }
                else if (args[i] == "--pr4-exit-after" && i + 1 < args.Length && TryParseFloat(args[i + 1], out var exitAfter))
                {
                    _exitAfterSeconds = Mathf.Max(0.2f, exitAfter);
                    i++;
                }
                else if (args[i] == "--pr4-host")
                {
                    startHost = true;
                }
                else if (args[i] == "--pr4-client")
                {
                    startClient = true;
                }
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            int steamAddress = Array.IndexOf(args, "--pr7-test-steam-address");
#endif
            if (!Application.isBatchMode)
                gameObject.AddComponent<SessionPanel>().Initialize(_sessions, address, port);

            if (startHost)
            {
                StartHost();
            }
            else if (startClient)
            {
                StartClient();
            }
            else if (Array.IndexOf(args, "--pr7-find-servers") >= 0)
            {
                _sessions.Search();
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            else if (steamAddress >= 0 && steamAddress + 1 < args.Length)
            {
                _sessions.TestSteamEndpoint(args[steamAddress + 1], port);
            }
#endif

            if (!string.IsNullOrWhiteSpace(_screenshotPath) || _exitAfterSeconds > 0.0f)
            {
                StartCoroutine(CaptureAndExit());
            }
#endif
        }

        private void StartDedicated(string[] args)
        {
            try
            {
                _dedicated = DedicatedServerSettings.Parse(args);
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != DedicatedServerSettings.Scene)
                    throw new InvalidOperationException($"Dedicated server requires scene {DedicatedServerSettings.Scene}");
                Debug.unityLogger.filterLogType = _dedicated.LogLevel;
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                _stopFile = Environment.GetEnvironmentVariable("DUSTLINE_STOP_FILE");

                var meter = _networkManager.NetworkConfig.NetworkTransport as MeteredTransport ??
                    throw new InvalidOperationException("Secure game transport is unavailable");
                if (ReplicationLab.Instance != null) ReplicationLab.Instance.Optimized = true;
                if (_dedicated.Steam)
                {
                    _networkManager.NetworkConfig.ConnectionApproval = true;
                    _networkManager.ConnectionApprovalCallback = ApprovePlayer;
                    _serverSteam = SteamSessionPlatform.Create?.Invoke() ??
                        throw new InvalidOperationException("Steam server is unavailable on this platform");
                    _serverSteam.InitializeServer(_dedicated);
                    meter.DatagramFactory = () => _serverSteam.CreateSocket(null);
                }
                meter.SetConnectionData("127.0.0.1", _dedicated.Port, _dedicated.ListenAddress.ToString());
                if (!_networkManager.StartServer() || _networkManager.IsHost)
                    throw new InvalidOperationException("NGO dedicated startup failed");
                _serverSteam?.Advertise();
                _status = $"Dedicated server listening on {_dedicated.ListenAddress}:{_dedicated.Port}";
                string readyFile = Environment.GetEnvironmentVariable("DUSTLINE_READY_FILE");
                if (!string.IsNullOrEmpty(readyFile)) File.WriteAllText(readyFile, "ready");
                Debug.Log($"[PR7] SERVER_READY name={_dedicated.Name} map={DedicatedServerSettings.Map} " +
                    $"profile={(_dedicated.Steam ? "steam" : "ip")} transport={(_dedicated.Steam ? "SteamNetworkingSockets+secure-UTP" : "secure-UTP/UDP")} " +
                    $"bind={_dedicated.ListenAddress}:{_dedicated.Port} max={_dedicated.MaxPlayers} clients={_networkManager.ConnectedClients.Count} localPlayer=false");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PR7] SERVER_ERROR {e.Message}");
                if (_networkManager.IsListening && !_networkManager.ShutdownInProgress) _networkManager.Shutdown();
                ReleaseRegistration();
                Application.Quit(2);
            }
        }

        private void ApprovePlayer(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (_dedicated.Steam && !SessionCoordinator.AcceptsSteamBuild(request.Payload))
                response.Reason = "Incompatible DUSTLINE build or network profile";
            else if (!ReserveSlot(request.ClientNetworkId))
                response.Reason = "Server is full";
            else
            {
                response.Approved = true;
                response.CreatePlayerObject = false;
            }
        }

        private bool ReserveSlot(ulong clientId)
        {
            for (int slot = 0; slot < _dedicated.MaxPlayers; slot++)
            {
                if (_spawnSlots.ContainsValue(slot)) continue;
                _spawnSlots.Add(clientId, slot);
                return true;
            }
            return false;
        }

        private void Update()
        {
            _sessions?.Tick();
            _serverSteam?.Pump();
            if (string.IsNullOrEmpty(_stopFile) || _stopping || Time.realtimeSinceStartupAsDouble < _nextStopCheck) return;
            _nextStopCheck = Time.realtimeSinceStartupAsDouble + 0.25;
            if (File.Exists(_stopFile))
            {
                _stopping = true;
                Debug.Log("[PR7] SERVER_STOP_REQUEST");
                if (_networkManager.IsListening) _networkManager.Shutdown();
                else OnServerStopped(false);
            }
        }

        private void OnServerStopped(bool wasHost)
        {
            _spawnSlots.Clear();
            ReleaseRegistration();
            if (_dedicated == null) return;
            Debug.Log("[PR7] SERVER_STOPPED");
            Application.Quit(_stopping ? 0 : 2);
        }

        private void ReleaseRegistration()
        {
            _serverSteam?.Dispose();
            _serverSteam = null;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private IEnumerator CaptureAndExit()
        {
            if (_waitForPlayers > 0)
            {
                var deadline = Time.realtimeSinceStartup + 90;
                while (_networkManager.IsServer ? _networkManager.ConnectedClientsList.Count < _waitForPlayers :
                    (!_networkManager.IsConnectedClient || FindObjectsByType<PredictedPlayer>(FindObjectsSortMode.None).Length < _waitForPlayers))
                {
                    if (Time.realtimeSinceStartup > deadline)
                    { Debug.LogWarning("[PR6] SCENARIO_CONNECT_TIMEOUT"); Application.Quit(2); yield break; }
                    yield return null;
                }
                Debug.Log("[PR6] SCENARIO_READY");
            }
            var captureAt = string.IsNullOrWhiteSpace(_screenshotPath)
                ? -1.0f
                : (_screenshotAtSeconds > 0.0f ? _screenshotAtSeconds : 2.5f);

            if (captureAt > 0.0f)
            {
                yield return new WaitForSecondsRealtime(captureAt);
                var directory = Path.GetDirectoryName(_screenshotPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                ScreenCapture.CaptureScreenshot(_screenshotPath);
                Debug.Log($"[PR4][Evidence] screenshot={_screenshotPath}");
            }

            if (_exitAfterSeconds > 0.0f)
            {
                var remaining = _exitAfterSeconds - Mathf.Max(0.0f, captureAt);
                if (remaining > 0.0f)
                {
                    yield return new WaitForSecondsRealtime(remaining);
                }

                Debug.Log("[PR4][Evidence] automatic demo complete; quitting");
                Application.Quit();
            }
        }

        private void OnDestroy()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientConnectedCallback -= OnClientConnected;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            _networkManager.OnServerStopped -= OnServerStopped;
            _sessions?.Dispose();
            ReleaseRegistration();
        }

        public void StartHost()
        {
            if (_dedicated != null || !_sessions.CanStart)
            {
                return;
            }

            _transport.SetConnectionData(address, port, "0.0.0.0");
            GetComponent<MeteredTransport>()?.SetConnectionData(address, port, "0.0.0.0");
            _status = _networkManager.StartHost() ? "Host started" : "Host start failed";
            Debug.Log($"[PR4][Bootstrap] {_status} on {address}:{port}");
        }

        public void StartClient()
        {
            if (_dedicated == null) _sessions.JoinDirect(address, port);
        }

        private void OnClientConnected(ulong clientId)
        {
            _status = $"Connected: client {clientId}";
            if (!_networkManager.IsServer)
            {
                _sessions.Connected(clientId);
                if (clientId == _networkManager.LocalClientId) Debug.Log($"[PR6] LOCAL_CLIENT id={clientId}");
                return;
            }

            if (_dedicated != null)
            {
                if (clientId == NetworkManager.ServerClientId) return;
                if (!_dedicated.Steam && !_spawnSlots.ContainsKey(clientId) && !ReserveSlot(clientId))
                {
                    _networkManager.DisconnectClient(clientId, "Server is full");
                    return;
                }
                if (!_spawnSlots.ContainsKey(clientId)) return;
            }
            if (_networkManager.ConnectedClients[clientId].PlayerObject != null)
            {
                return;
            }

            var spawn = new Vector3(-2.5f + (clientId % 4) * 2.5f, 1.0f, -4.0f);
            if (_dedicated != null)
            {
                int slot = _spawnSlots[clientId];
                spawn = new Vector3(-2.5f + (slot % 4) * 2.5f, 1f, -4f - (slot / 4) * 3f);
            }
            var instance = Instantiate(playerPrefab, spawn, Quaternion.identity);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[PR4][Server] Spawned authoritative player for client={clientId} at {spawn}");
            if (_dedicated != null)
            {
                Debug.Log($"[PR7] PLAYER_JOIN client={clientId} count={_spawnSlots.Count}");
                _serverSteam?.UpdatePlayers(_spawnSlots.Count);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _status = $"Disconnected: client {clientId}";
            if (_dedicated != null)
            {
                if (!_spawnSlots.Remove(clientId)) return;
                Debug.Log($"[PR7] PLAYER_LEAVE client={clientId} count={_spawnSlots.Count}");
                _serverSteam?.UpdatePlayers(_spawnSlots.Count);
            }
            else if (!_networkManager.IsServer) _sessions.Disconnected(clientId);
        }

        public string Status => _networkManager.IsServer || _sessions == null || _sessions.Status == "Ready" ? _status : _sessions.Status;
        public string Address { get => address; set => address = value; }

        private void OnGUI()
        {
            if (!TankGameHud.DiagnosticsVisible) return;
            var previousGuiMatrix = PredictionHudLayout.BeginScaledGui();
            try
            {
                GUILayout.BeginArea(new Rect(16, 16, 470, 335), GUI.skin.box);
                GUILayout.Label("PR4 — Client-Side Prediction / Server Reconciliation (Unity)");
                GUILayout.Label($"Status: {Status}");

                if (!_networkManager.IsListening)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Address", GUILayout.Width(60));
                    address = GUILayout.TextField(address, GUILayout.Width(160));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Start Host", GUILayout.Width(120))) StartHost();
                    if (GUILayout.Button("Start Client", GUILayout.Width(120))) StartClient();
                    GUILayout.EndHorizontal();
                }
                else
                {
                    var role = _networkManager.IsHost ? "Listen Server (Host)" : _networkManager.IsServer ? "Server" : "Client";
                    GUILayout.Label($"Role: {role}");
                }

                GUILayout.Space(6);
                GUILayout.Label("Network emulation profile (logical one-way path):");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("0 ms / 0%")) NetworkLabSettings.ActiveProfile = LabProfile.Local;
                if (GUILayout.Button("100 ms / 0%")) NetworkLabSettings.ActiveProfile = LabProfile.Lag100;
                if (GUILayout.Button("175 ms / 5%")) NetworkLabSettings.ActiveProfile = LabProfile.Lag175Loss5;
                GUILayout.EndHorizontal();
                GUILayout.Label($"Active: {NetworkLabSettings.Describe(NetworkLabSettings.ActiveProfile)}");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Packet loss: {NetworkLabSettings.GetProfile(NetworkLabSettings.ActiveProfile).LossPercent}%", GUILayout.Width(145));
                var loss = Mathf.RoundToInt(GUILayout.HorizontalSlider(NetworkLabSettings.GetProfile(NetworkLabSettings.ActiveProfile).LossPercent, 0, 100));
                if (loss != NetworkLabSettings.GetProfile(NetworkLabSettings.ActiveProfile).LossPercent)
                {
                    NetworkLabSettings.CustomDelayMs = NetworkLabSettings.GetProfile(NetworkLabSettings.ActiveProfile).OneWayDelayMs;
                    NetworkLabSettings.CustomLossPercent = loss;
                    NetworkLabSettings.ActiveProfile = LabProfile.Custom;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Logical loss affects movement, state and shot messages. 100% blocks the path.");

                if (GUILayout.Button(NetworkLabSettings.PredictionEnabled ? "Prediction: ON" : "Prediction: OFF"))
                {
                    NetworkLabSettings.PredictionEnabled = !NetworkLabSettings.PredictionEnabled;
                }

                GUILayout.Label("Controls: WASD = move, Space = dash, B = divergence, LMB / F = fire.");
                GUILayout.Label("B demonstrates correction: server never receives client coordinates or dash distance.");
                if (NetworkLabSettings.AutomationEnabled)
                {
                    GUILayout.Label($"Automation: ON | divergence={NetworkLabSettings.AutomationDivergence}");
                }
                GUILayout.EndArea();
            }
            finally
            {
                PredictionHudLayout.EndScaledGui(previousGuiMatrix);
            }
        }
    }
}
