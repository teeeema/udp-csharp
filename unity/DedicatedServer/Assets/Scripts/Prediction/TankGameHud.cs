using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace PredictionDemo
{
    public sealed class TankGameHud : MonoBehaviour
    {
        public static bool DiagnosticsVisible { get; private set; }
        private Canvas _canvas;
        private GameObject _menu, _info, _leave;
        private TMP_Text _status, _position, _profile, _boostLabel;
        private TMP_InputField _address;
        private UnityEngine.UI.Image _boost;
        private NetworkBootstrap _bootstrap;
        private PredictedPlayer _player;
        private TMP_FontAsset _font;
        private readonly Color _ink = new Color(.045f, .07f, .08f, .95f);
        private readonly Color _mint = new Color(.32f, .92f, .79f);

        private void Awake()
        {
            DiagnosticsVisible = false;
            _bootstrap = GetComponent<NetworkBootstrap>();
            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var canvasObject = new GameObject("Test range HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var title = Panel("Brand", canvasObject.transform, new Vector2(24, -24), new Vector2(390, 98), new Vector2(0, 1));
            Label(title, "D U S T L I N E", 30, new Vector2(20, -14), new Vector2(360, 40), Color.white);
            Label(title, "OPEN WORLD    /    NETWORK TEST RANGE", 12, new Vector2(22, -61), new Vector2(350, 24), _mint);
            var state = Panel("Connection", canvasObject.transform, new Vector2(-24, -24), new Vector2(330, 66), new Vector2(1, 1));
            _status = Label(state, "SYSTEM READY", 16, new Vector2(18, -14), new Vector2(300, 42), _mint);
            _menu = Panel("Session", canvasObject.transform, new Vector2(24, 24), new Vector2(390, 450), new Vector2(0, 0)).gameObject;
            Label(_menu.transform, "NETWORK TEST SESSION", 23, new Vector2(22, -24), new Vector2(350, 40), Color.white);
            Label(_menu.transform, "Explore the open test range freely.\nTest movement, shooting and network delay.", 16, new Vector2(22, -76), new Vector2(346, 64), new Color(.78f, .83f, .8f));
            Button(_menu.transform, "START HOST / SOLO TEST", new Vector2(22, -160), new Vector2(346, 50), () => _bootstrap.StartHost(), true);
            Label(_menu.transform, "JOIN ANOTHER INSTANCE  /  IP ADDRESS", 12, new Vector2(22, -231), new Vector2(346, 20), _mint);
            _address = Input(_menu.transform, new Vector2(22, -261), new Vector2(216, 44));
            Button(_menu.transform, "JOIN", new Vector2(248, -261), new Vector2(120, 44), () => { _bootstrap.Address = _address.text.Trim(); _bootstrap.StartClient(); });
            _profile = Button(_menu.transform, "NETWORK / LOCAL", new Vector2(22, -325), new Vector2(346, 38), () =>
                NetworkLabSettings.ActiveProfile = (LabProfile)(((int)NetworkLabSettings.ActiveProfile + 1) % 3));
            Label(_menu.transform, "F3  NETWORK LAB & PREDICTION SETTINGS", 13, new Vector2(22, -391), new Vector2(346, 40), Color.white);
            _info = Panel("FreeRoam", canvasObject.transform, new Vector2(24, 24), new Vector2(390, 112), new Vector2(0, 0)).gameObject;
            Label(_info.transform, "FREE ROAM / TEST SANDBOX", 20, new Vector2(20, -16), new Vector2(350, 32), Color.white);
            _position = Label(_info.transform, "", 15, new Vector2(20, -58), new Vector2(350, 34), _mint);
            var controls = Panel("Controls", canvasObject.transform, new Vector2(-24, 24), new Vector2(390, 132), new Vector2(1, 0));
            Label(controls, "WASD MOVE   SPACE DASH   LMB / F FIRE", 16, new Vector2(18, -14), new Vector2(355, 30), Color.white);
            Label(controls, "B  DIVERGENCE TEST    F3  NETWORK LAB", 12, new Vector2(18, -48), new Vector2(355, 24), _mint);
            _boostLabel = Label(controls, "DASH READY", 12, new Vector2(18, -80), new Vector2(355, 24), Color.white);
            var boostBack = Panel("Boost", controls, new Vector2(18, -113), new Vector2(354, 5), new Vector2(0, 1));
            _boost = Panel("Charge", boostBack, Vector2.zero, new Vector2(354, 5), new Vector2(0, 1)).GetComponent<UnityEngine.UI.Image>(); _boost.color = _mint;
            _leave = Button(canvasObject.transform, "LEAVE SESSION", new Vector2(-24, -104), new Vector2(190, 40), () => NetworkManager.Singleton.Shutdown()).transform.parent.gameObject;
            var leaveRect = _leave.GetComponent<RectTransform>(); leaveRect.anchorMin = leaveRect.anchorMax = leaveRect.pivot = new Vector2(1, 1);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) DiagnosticsVisible = !DiagnosticsVisible;
            _canvas.enabled = !DiagnosticsVisible;
            var manager = NetworkManager.Singleton;
            bool connected = manager != null && manager.IsConnectedClient;
            _menu.SetActive(!connected); _info.SetActive(connected); _leave.SetActive(connected);
            _status.text = connected ? $"{(manager.IsHost ? "HOST" : "CLIENT")}   /   {manager.ConnectedClientsIds.Count} CONNECTED" : _bootstrap.Status.ToUpperInvariant();
            _profile.text = NetworkLabSettings.ActiveProfile == LabProfile.Custom ? $"NETWORK / CUSTOM : {NetworkLabSettings.CustomDelayMs} ms, {NetworkLabSettings.CustomLossPercent}%" : NetworkLabSettings.ActiveProfile == LabProfile.Local ? "NETWORK / LOCAL : 0 ms, 0%" : NetworkLabSettings.ActiveProfile == LabProfile.Lag100 ? "NETWORK / DELAY : 100 ms, 0%" : "NETWORK / STRESS : 175 ms, 5%";
            if (!connected) { _player = null; return; }
            if (_player == null && manager.LocalClient.PlayerObject != null) _player = manager.LocalClient.PlayerObject.GetComponent<PredictedPlayer>();
            if (_player == null || !_player.IsSpawned) return;
            var p = _player.transform.position;
            _position.text = $"PLAYER {_player.OwnerClientId}   /   X {p.x:F1}   Z {p.z:F1}";
            var cooldown = _player.DashCooldownRemaining;
            var weapon = _player.GetComponent<TankWeapon>();
            _boostLabel.text = (weapon != null ? $"FIRE {weapon.ShotsFired.Value} / HITS {weapon.Hits.Value}  |  " : "") + (cooldown > .01f ? $"DASH RECHARGING  /  {cooldown:F1}s" : "DASH READY");
            _boost.rectTransform.sizeDelta = new Vector2(354 * (1 - cooldown / PredictionRules.DashCooldownSeconds), 5);
        }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = _ink; image.raycastTarget = false; return rect;
        }

        private TMP_Text Label(Transform parent, string content, float size, Vector2 position, Vector2 dimensions, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = _font; text.text = content; text.fontSize = size; text.color = color;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            return text;
        }

        private TMP_Text Button(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var panel = Panel(text, parent, position, size, new Vector2(0, 1));
            var image = panel.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true; image.color = primary ? _mint : new Color(.16f, .23f, .23f);
            var button = panel.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(.78f, 1, .93f); colors.pressedColor = new Color(.5f, .75f, .68f); button.colors = colors;
            var label = Label(panel, text, 16, Vector2.zero, size, primary ? _ink : Color.white); label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private TMP_InputField Input(Transform parent, Vector2 position, Vector2 size)
        {
            var panel = Panel("Address", parent, position, size, new Vector2(0, 1));
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var text = (TextMeshProUGUI)Label(panel, "127.0.0.1", 18, new Vector2(12, -5), size - new Vector2(24, 10), Color.white);
            var field = panel.gameObject.AddComponent<TMP_InputField>(); field.textViewport = panel; field.textComponent = text; field.text = "127.0.0.1"; field.characterLimit = 253;
            return field;
        }

        private void OnDestroy() { DiagnosticsVisible = false; }
    }
}
