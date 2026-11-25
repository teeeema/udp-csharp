using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PredictionDemo
{
    internal sealed class SessionPanel : MonoBehaviour
    {
        private SessionCoordinator sessions;
        private SessionInfo selected;
        private string host;
        private string port;
        private string inputError;
        private bool opened;
        private Vector2 scroll;
        private SessionState lastState;

        public void Initialize(SessionCoordinator coordinator, string address, ushort gamePort)
        {
            sessions = coordinator;
            host = address;
            port = gamePort.ToString();
        }

        private void Update()
        {
            if (sessions == null) return;
            if (sessions.State == SessionState.InGame && lastState != SessionState.InGame) opened = false;
            if (Keyboard.current?.f2Key.wasPressedThisFrame == true) opened = !opened;
            lastState = sessions.State;
        }

        private void OnGUI()
        {
            if (sessions == null) return;
            var matrix = PredictionHudLayout.BeginScaledGui();
            var priorColor = GUI.color;
            var priorEnabled = GUI.enabled;
            try
            {
                GUI.color = new Color32(249, 243, 227, 255);
                if (!opened)
                {
                    if (sessions.CanStart && GUI.Button(new Rect(765, 70, 180, 32), "F2  Server Browser"))
                        opened = true;
                    return;
                }
                GUI.color = new Color32(21, 29, 35, 249);
                GUI.DrawTexture(new Rect(438, 117, 506, 332), Texture2D.whiteTexture);
                GUI.color = new Color32(255, 181, 77, 255);
                GUI.DrawTexture(new Rect(438, 117, 506, 4), Texture2D.whiteTexture);
                GUI.color = new Color32(249, 243, 227, 255);
                GUILayout.BeginArea(new Rect(450, 130, 482, 308));
                GUILayout.BeginHorizontal();
                GUILayout.Label("DUSTLINE  /  SERVER BROWSER", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Close", GUILayout.Width(70))) opened = false;
                GUILayout.EndHorizontal();
                GUILayout.Label(sessions.SteamStatus);
                GUILayout.Label($"{sessions.State}  /  {inputError ?? sessions.Status}");
                GUILayout.BeginHorizontal();
                GUI.enabled = sessions.CanStart;
                bool find = GUILayout.Button("Find Servers");
                bool refresh = GUILayout.Button("Refresh");
                if (find || refresh)
                {
                    inputError = null;
                    selected = null;
                    sessions.Search();
                }
                GUI.enabled = sessions.State == SessionState.ResultsReady && selected != null;
                if (GUILayout.Button("Join Server")) sessions.Join(selected);
                GUI.enabled = sessions.State == SessionState.InGame || sessions.State == SessionState.Joining ||
                    sessions.State == SessionState.Searching;
                if (GUILayout.Button(sessions.State == SessionState.Searching ? "Cancel Search" : "Disconnect"))
                {
                    inputError = null;
                    sessions.Leave();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(145));
                foreach (var server in sessions.Servers)
                {
                    var label = $"{server.Name}   /   {server.Map}   /   {server.Mode}   /   {server.Players}/{server.MaxPlayers}";
                    if (GUILayout.Toggle(ReferenceEquals(selected, server), label, GUI.skin.button))
                        selected = server;
                }
                GUILayout.EndScrollView();

                GUILayout.Label("DIRECT IP  /  DIAGNOSTICS");
                GUILayout.BeginHorizontal();
                GUI.enabled = sessions.CanStart;
                host = GUILayout.TextField(host, 253, GUILayout.Width(240));
                port = GUILayout.TextField(port, 5, GUILayout.Width(55));
                if (GUILayout.Button("Direct IP Connect"))
                {
                    inputError = null;
                    try { sessions.JoinDirect(host.Trim(), DedicatedServerSettings.ParsePort(port)); }
                    catch (ArgumentException e) { inputError = e.Message; }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.enabled = priorEnabled;
                GUI.color = priorColor;
                PredictionHudLayout.EndScaledGui(matrix);
            }
        }
    }
}
