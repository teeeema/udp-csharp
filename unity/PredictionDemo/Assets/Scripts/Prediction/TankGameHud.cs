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

        private static readonly Color Ink = new Color32(21, 29, 35, 250);
        private static readonly Color Surface = new Color32(38, 49, 56, 255);
        private static readonly Color Ivory = new Color32(249, 243, 227, 255);
        private static readonly Color Muted = new Color32(182, 199, 193, 255);
        private static readonly Color Amber = new Color32(255, 181, 77, 255);
        private static readonly Color Teal = new Color32(95, 222, 208, 255);

        private Canvas _canvas;
        private RectTransform _menu, _match, _playerPanel;
        private TMP_Text _status, _connection, _playerId, _fireStats, _boostLabel;
        private TMP_InputField _address;
        private RectTransform _boost;
        private NetworkBootstrap _bootstrap;
        private PredictedPlayer _player;
        private TankWeapon _weapon;
        private TMP_FontAsset _font;

        private void Awake()
        {
            DiagnosticsVisible = false;
            _bootstrap = GetComponent<NetworkBootstrap>();
            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var canvasObject = new GameObject("Dustline HUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            BuildMenu(canvasObject.transform);
            BuildMatchHud(canvasObject.transform);
        }

        private void BuildMenu(Transform canvas)
        {
            var anchor = new Vector2(0f, .5f);
            _menu = Rect("Session menu", canvas, anchor, anchor, new Vector2(32, 0), new Vector2(370, 400));
            Fill(_menu, Ink);
            Stripe(_menu, 370);

            Label(_menu, "MULTIPLAYER TANK ARENA", 13, 24, -24, 322, 20, Amber, true).characterSpacing = 4;
            Label(_menu, "DUSTLINE", 54, 21, -46, 328, 66, Ivory, true);
            Label(_menu, "Copper Basin", 20, 24, -118, 300, 28, Muted, true);

            Button(_menu, "HOST GAME", 24, -164, 322, 47, _bootstrap.StartHost, true);
            Label(_menu, "SERVER ADDRESS", 12, 24, -230, 210, 18, Muted, true).characterSpacing = 3;
            _address = Input(_menu, 24, -252, 226, 42);
            Button(_menu, "JOIN", 258, -252, 88, 42, () =>
            {
                _bootstrap.Address = _address.text.Trim();
                _bootstrap.StartClient();
            });
            _status = Label(_menu, "", 14, 24, -312, 322, 34, Teal);
            Label(_menu, "F3  Network lab       F4  Replication", 13, 24, -363, 322, 21, Muted);
        }

        private void BuildMatchHud(Transform canvas)
        {
            _match = Rect("Match HUD", canvas, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _match.anchorMax = Vector2.one;
            var top = new Vector2(.5f, 1);
            var status = Rect("Session status", _match, top, top, new Vector2(0, -20), new Vector2(284, 44));
            Fill(status, Ink);
            Stripe(status, 284);
            _connection = Label(status, "", 14, 0, -3, 284, 41, Ivory, true);
            _connection.alignment = TextAlignmentOptions.Center;

            var right = new Vector2(1, 1);
            var leave = Rect("Session actions", _match, right, right, new Vector2(-24, -20), new Vector2(108, 34));
            Button(leave, "LEAVE", 0, 0, 108, 34, () => NetworkManager.Singleton.Shutdown());

            _playerPanel = Rect("Tank status", _match, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(258, 110));
            Fill(_playerPanel, Ink);
            Stripe(_playerPanel, 258);
            _playerId = Label(_playerPanel, "", 17, 16, -12, 226, 25, Teal, true);
            _fireStats = Label(_playerPanel, "", 14, 16, -41, 226, 22, Ivory);
            _boostLabel = Label(_playerPanel, "", 12, 16, -68, 226, 18, Muted, true);
            var track = Rect("Dash track", _playerPanel, Vector2.up, Vector2.up, new Vector2(16, -93), new Vector2(226, 5));
            Fill(track, Surface);
            _boost = Rect("Dash charge", track, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _boost.anchorMax = Vector2.one;
            Fill(_boost, Amber);

            var bottomRight = Vector2.right;
            var controls = Rect("Controls", _match, bottomRight, bottomRight, new Vector2(-24, 24), new Vector2(332, 66));
            Fill(controls, Ink);
            Label(controls, "WASD Move   Space Dash   F / LMB Fire", 14, 12, -10, 308, 20, Ivory, true);
            Label(controls, "F3   Network lab       F4   Replication", 14, 12, -36, 308, 20, Muted);
        }

        private void Update()
        {
            if (Keyboard.current?.f3Key.wasPressedThisFrame == true)
            {
                DiagnosticsVisible = !DiagnosticsVisible;
                if (DiagnosticsVisible && EventSystem.current?.currentSelectedGameObject == _address.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                else if (!DiagnosticsVisible && !_address.isFocused)
                    _address.text = _bootstrap.Address;
            }

            _canvas.enabled = !DiagnosticsVisible;
            var manager = NetworkManager.Singleton;
            bool connected = manager != null && manager.IsConnectedClient;
            _menu.gameObject.SetActive(!connected);
            _match.gameObject.SetActive(connected);
            _status.text = _bootstrap.Status;
            if (!connected)
            {
                _player = null;
                _weapon = null;
                return;
            }

            if (_player == null && manager.LocalClient.PlayerObject != null)
            {
                _player = manager.LocalClient.PlayerObject.GetComponent<PredictedPlayer>();
                _weapon = manager.LocalClient.PlayerObject.GetComponent<TankWeapon>();
            }

            var count = ReplicationLab.Instance != null ? ReplicationLab.Instance.PlayerCount : 0;
            _connection.text = $"{(manager.IsHost ? "HOST" : "CLIENT")}   /   {count} IN ARENA";
            bool hasTank = _player != null && _player.IsSpawned;
            _playerPanel.gameObject.SetActive(hasTank);
            if (!hasTank) return;

            _playerId.text = $"TANK {_player.OwnerClientId + 1:00}";
            _fireStats.text = _weapon == null ? string.Empty : $"SHOTS  {_weapon.ShotsFired.Value}     HITS  {_weapon.Hits.Value}";
            float cooldown = _player.DashCooldownRemaining;
            _boostLabel.text = cooldown > .01f ? $"DASH   {cooldown:F1}s" : "DASH READY";
            _boost.anchorMax = new Vector2(Mathf.Clamp01(1f - cooldown / PredictionRules.DashCooldownSeconds), 1);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static UnityEngine.UI.Image Fill(RectTransform rect, Color color, bool interactive = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = interactive;
            return image;
        }

        private static void Stripe(Transform parent, float width)
        {
            Fill(Rect("Accent", parent, Vector2.up, Vector2.up, Vector2.zero, new Vector2(width, 3)), Amber);
        }

        private TextMeshProUGUI Label(Transform parent, string content, float size, float x, float y,
            float width, float height, Color color, bool bold = false)
        {
            var rect = Rect(content.Length > 0 ? content : "Text", parent, Vector2.up, Vector2.up,
                new Vector2(x, y), new Vector2(width, height));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private void Button(Transform parent, string title, float x, float y, float width, float height,
            UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var rect = Rect(title, parent, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(width, height));
            var image = Fill(rect, primary ? Amber : Surface, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var colors = button.colors;
            colors.normalColor = new Color(.93f, .93f, .93f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.75f, .75f, .75f);
            button.colors = colors;
            Label(rect, title, 15, 0, 0, width, height, primary ? Ink : Ivory, true).alignment = TextAlignmentOptions.Center;
        }

        private TMP_InputField Input(Transform parent, float x, float y, float width, float height)
        {
            var rect = Rect("Server address", parent, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(width, height));
            var image = Fill(rect, Surface, true);
            var text = Label(rect, "", 17, 12, 0, width - 24, height, Ivory);
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = image;
            field.textViewport = rect;
            field.textComponent = text;
            field.text = _bootstrap.Address;
            field.characterLimit = 253;
            return field;
        }

        private void OnDestroy()
        {
            DiagnosticsVisible = false;
        }
    }
}
