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

        private void Awake()
        {
            Application.runInBackground = true;
            _networkManager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            _networkManager.NetworkConfig.EnableSceneManagement = false;
            _networkManager.AddNetworkPrefab(playerPrefab);
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private IEnumerator Start()
        {
            yield return null;

            var args = Environment.GetCommandLineArgs();
            var startHost = false;
            var startClient = false;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--pr4-profile" && i + 1 < args.Length && byte.TryParse(args[i + 1], out var p))
                {
                    NetworkLabSettings.ActiveProfile = (LabProfile)Mathf.Clamp(p, 0, 2);
                    i++;
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

            if (startHost)
            {
                StartHost();
            }
            else if (startClient)
            {
                StartClient();
            }

            if (!string.IsNullOrWhiteSpace(_screenshotPath) || _exitAfterSeconds > 0.0f)
            {
                StartCoroutine(CaptureAndExit());
            }
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private IEnumerator CaptureAndExit()
        {
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
        }

        public void StartHost()
        {
            if (_networkManager.IsListening)
            {
                return;
            }

            _transport.SetConnectionData(address, port, "0.0.0.0");
            _status = _networkManager.StartHost() ? "Host started" : "Host start failed";
            Debug.Log($"[PR4][Bootstrap] {_status} on {address}:{port}");
        }

        public void StartClient()
        {
            if (_networkManager.IsListening)
            {
                return;
            }

            _transport.SetConnectionData(address, port);
            _status = _networkManager.StartClient() ? "Client connecting" : "Client start failed";
            Debug.Log($"[PR4][Bootstrap] {_status} to {address}:{port}");
        }

        private void OnClientConnected(ulong clientId)
        {
            _status = $"Connected: client {clientId}";
            if (!_networkManager.IsServer)
            {
                return;
            }

            if (_networkManager.ConnectedClients[clientId].PlayerObject != null)
            {
                return;
            }

            var spawn = new Vector3(-2.5f + (clientId % 4) * 2.5f, 1.0f, -4.0f);
            var instance = Instantiate(playerPrefab, spawn, Quaternion.identity);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[PR4][Server] Spawned authoritative player for client={clientId} at {spawn}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _status = $"Disconnected: client {clientId}";
        }

        public string Status => _status;
        public string Address { get => address; set => address = value; }

        private void OnGUI()
        {
            if (!TankGameHud.DiagnosticsVisible) return;
            var previousGuiMatrix = PredictionHudLayout.BeginScaledGui();
            try
            {
                GUILayout.BeginArea(new Rect(16, 16, 470, 335), GUI.skin.box);
                GUILayout.Label("PR4 — Client-Side Prediction / Server Reconciliation (Unity)");
                GUILayout.Label($"Status: {_status}");

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
