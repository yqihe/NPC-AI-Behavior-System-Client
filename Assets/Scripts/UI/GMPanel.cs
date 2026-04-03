using System.Globalization;
using NpcAi.Network;
using NpcAi.Network.Messages;
using NpcAi.Visualization;
using UnityEngine;
using UnityEngine.UI;

namespace NpcAi.UI
{
    /// <summary>
    /// GM debug panel. Provides controls for spawning/removing NPCs, publishing events,
    /// and displays connection status + tick info.
    /// Built entirely in code — no prefab needed.
    /// </summary>
    public class GMPanel : MonoBehaviour
    {
        [SerializeField] private NpcClient npcClient;
        [SerializeField] private NpcManager npcManager;

        // ---- UI references (built at runtime) ----
        private Text _statusText;
        private InputField _npcIdInput;
        private Dropdown _npcTypeDropdown;
        private InputField _xInput;
        private InputField _zInput;
        private Dropdown _eventTypeDropdown;
        private InputField _severityInput;
        private Text _logText;
        private ScrollRect _logScroll;
        private RectTransform _panelRect;
        private bool _collapsed;

        private int _npcCounter;
        private int _logLineCount;
        private const int MaxLogLines = 50;

        private void Start()
        {
            if (npcClient == null) npcClient = FindFirstObjectByType<NpcClient>();
            if (npcManager == null) npcManager = FindFirstObjectByType<NpcManager>();

            BuildUI();

            npcClient.OnConnectionStateChanged += connected =>
                _statusText.text = connected ? "<color=green>Connected</color>" : "<color=red>Disconnected</color>";
            npcClient.OnWorldSnapshot += snap =>
                _statusText.text = $"<color=green>Connected</color>  Tick: {snap.tick}  NPCs: {snap.npcs.Length}";
        }

        // ---- UI Construction ----

        private void BuildUI()
        {
            // Canvas (if not already present)
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // EventSystem
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Panel background
            var panel = CreatePanel(canvas.transform, "GMPanel",
                new Vector2(0, 1), new Vector2(0, 1), // top-left anchor
                new Vector2(10, -10), new Vector2(320, 520));
            _panelRect = panel.GetComponent<RectTransform>();

            float y = -5f;

            // Title + collapse button
            var titleRow = CreateRow(panel.transform, ref y);
            CreateLabel(titleRow.transform, "GM Panel", 14, FontStyle.Bold, new Vector2(0, 0), new Vector2(220, 25));
            var collapseBtn = CreateButton(titleRow.transform, "[-]", new Vector2(225, 0), new Vector2(60, 25));
            collapseBtn.onClick.AddListener(ToggleCollapse);

            // Status
            _statusText = CreateLabel(panel.transform, "<color=red>Disconnected</color>", 12, FontStyle.Normal,
                new Vector2(10, y -= 28), new Vector2(290, 20)).GetComponent<Text>();

            // ---- Spawn NPC section ----
            CreateLabel(panel.transform, "--- Spawn NPC ---", 11, FontStyle.Bold, new Vector2(10, y -= 25), new Vector2(290, 20));

            CreateLabel(panel.transform, "ID:", 11, FontStyle.Normal, new Vector2(10, y -= 22), new Vector2(30, 20));
            _npcIdInput = CreateInputField(panel.transform, "npc_1", new Vector2(40, y), new Vector2(130, 22));
            var autoIdBtn = CreateButton(panel.transform, "Auto", new Vector2(175, y), new Vector2(50, 22));
            autoIdBtn.onClick.AddListener(() => _npcIdInput.text = $"npc_{++_npcCounter}");

            CreateLabel(panel.transform, "Type:", 11, FontStyle.Normal, new Vector2(10, y -= 25), new Vector2(40, 20));
            _npcTypeDropdown = CreateDropdown(panel.transform, new[] { "civilian", "police" },
                new Vector2(55, y), new Vector2(120, 22));

            CreateLabel(panel.transform, "X:", 11, FontStyle.Normal, new Vector2(10, y -= 25), new Vector2(20, 20));
            _xInput = CreateInputField(panel.transform, "0", new Vector2(28, y), new Vector2(60, 22));
            _xInput.contentType = InputField.ContentType.DecimalNumber;
            CreateLabel(panel.transform, "Z:", 11, FontStyle.Normal, new Vector2(100, y), new Vector2(20, 20));
            _zInput = CreateInputField(panel.transform, "0", new Vector2(118, y), new Vector2(60, 22));
            _zInput.contentType = InputField.ContentType.DecimalNumber;

            var spawnBtn = CreateButton(panel.transform, "Spawn", new Vector2(10, y -= 28), new Vector2(90, 26));
            spawnBtn.onClick.AddListener(OnSpawn);
            var removeBtn = CreateButton(panel.transform, "Remove", new Vector2(105, y), new Vector2(90, 26));
            removeBtn.onClick.AddListener(OnRemove);

            // ---- Event section ----
            CreateLabel(panel.transform, "--- Publish Event ---", 11, FontStyle.Bold, new Vector2(10, y -= 30), new Vector2(290, 20));

            CreateLabel(panel.transform, "Event:", 11, FontStyle.Normal, new Vector2(10, y -= 22), new Vector2(45, 20));
            _eventTypeDropdown = CreateDropdown(panel.transform,
                new[] { "explosion", "gunshot", "shout", "fire" },
                new Vector2(55, y), new Vector2(120, 22));

            CreateLabel(panel.transform, "Sev:", 11, FontStyle.Normal, new Vector2(10, y -= 25), new Vector2(35, 20));
            _severityInput = CreateInputField(panel.transform, "0", new Vector2(45, y), new Vector2(60, 22));
            _severityInput.contentType = InputField.ContentType.DecimalNumber;
            CreateLabel(panel.transform, "(0=default)", 10, FontStyle.Italic, new Vector2(110, y), new Vector2(100, 20));

            var eventNote = CreateLabel(panel.transform, "Click map to set X/Z, or use fields above", 10, FontStyle.Italic,
                new Vector2(10, y -= 22), new Vector2(290, 20));

            var eventBtn = CreateButton(panel.transform, "Publish at X/Z", new Vector2(10, y -= 28), new Vector2(140, 26));
            eventBtn.onClick.AddListener(OnPublishEvent);

            // ---- Log ----
            CreateLabel(panel.transform, "--- Log ---", 11, FontStyle.Bold, new Vector2(10, y -= 30), new Vector2(290, 20));

            var logArea = new GameObject("LogArea", typeof(RectTransform));
            logArea.transform.SetParent(panel.transform, false);
            var logRect = logArea.GetComponent<RectTransform>();
            logRect.anchorMin = new Vector2(0, 0);
            logRect.anchorMax = new Vector2(0, 0);
            logRect.pivot = new Vector2(0, 1);
            logRect.anchoredPosition = new Vector2(10, y -= 22);
            logRect.sizeDelta = new Vector2(290, 100);

            _logScroll = logArea.AddComponent<ScrollRect>();
            var logBg = logArea.AddComponent<Image>();
            logBg.color = new Color(0, 0, 0, 0.3f);
            _logScroll.vertical = true;
            _logScroll.horizontal = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(logArea.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.anchoredPosition = Vector2.zero;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _logText = content.AddComponent<Text>();
            _logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _logText.fontSize = 11;
            _logText.color = Color.white;
            _logText.supportRichText = true;

            _logScroll.content = contentRect;
            logArea.AddComponent<Mask>().showMaskGraphic = true;

            AppendLog("GM Panel ready.");
        }

        // ---- Actions ----

        private void OnSpawn()
        {
            string npcId = _npcIdInput.text.Trim();
            if (string.IsNullOrEmpty(npcId)) { AppendLog("<color=red>NPC ID is empty</color>"); return; }

            string typeName = _npcTypeDropdown.options[_npcTypeDropdown.value].text;
            float.TryParse(_xInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(_zInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float z);

            AppendLog($"Spawning {typeName} '{npcId}' at ({x}, {z})...");
            npcClient.SpawnNpc(npcId, typeName, x, z,
                resp => AppendLog($"<color=green>Spawned {resp.npc_id}</color>"),
                err => AppendLog($"<color=red>Spawn failed: [{err.code}] {err.message}</color>"));
        }

        private void OnRemove()
        {
            string npcId = _npcIdInput.text.Trim();
            if (string.IsNullOrEmpty(npcId)) return;

            AppendLog($"Removing '{npcId}'...");
            npcClient.RemoveNpc(npcId,
                resp => AppendLog($"<color=green>Removed {resp.npc_id}</color>"),
                err => AppendLog($"<color=red>Remove failed: [{err.code}] {err.message}</color>"));
        }

        private void OnPublishEvent()
        {
            string eventType = _eventTypeDropdown.options[_eventTypeDropdown.value].text;
            float.TryParse(_xInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(_zInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
            float.TryParse(_severityInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float sev);

            AppendLog($"Publishing {eventType} at ({x}, {z}) sev={sev}...");
            npcClient.PublishEvent(eventType, x, z, sev, null,
                resp => AppendLog($"<color=green>Event published: {resp.event_id}</color>"),
                err => AppendLog($"<color=red>Event failed: [{err.code}] {err.message}</color>"));
        }

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            // Keep title row visible, hide everything else by resizing
            _panelRect.sizeDelta = _collapsed
                ? new Vector2(320, 35)
                : new Vector2(320, 520);
        }

        // ---- Map Click (publish event at clicked position) ----

        private void Update()
        {
            if (Input.GetMouseButtonDown(1) && Camera.main != null) // right-click
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, Vector3.zero);
                if (plane.Raycast(ray, out float dist))
                {
                    var point = ray.GetPoint(dist);
                    _xInput.text = point.x.ToString("F1", CultureInfo.InvariantCulture);
                    _zInput.text = point.z.ToString("F1", CultureInfo.InvariantCulture);
                    AppendLog($"Map click: ({point.x:F1}, {point.z:F1})");
                }
            }
        }

        // ---- Log ----

        private void AppendLog(string msg)
        {
            _logLineCount++;
            if (_logLineCount > MaxLogLines)
            {
                int idx = _logText.text.IndexOf('\n');
                if (idx >= 0) _logText.text = _logText.text.Substring(idx + 1);
            }
            _logText.text += (string.IsNullOrEmpty(_logText.text) ? "" : "\n") + msg;
            Canvas.ForceUpdateCanvases();
            _logScroll.verticalNormalizedPosition = 0f;
        }

        // ---- UI Helpers ----

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            return go;
        }

        private static GameObject CreateRow(Transform parent, ref float y)
        {
            y -= 2f;
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, y);
            rt.sizeDelta = new Vector2(-20, 25);
            return go;
        }

        private static GameObject CreateLabel(Transform parent, string text, int fontSize, FontStyle style,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = Color.white;
            t.supportRichText = true;
            return go;
        }

        private static InputField CreateInputField(Transform parent, string placeholder,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject("InputField", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(4, 0);
            textRt.offsetMax = new Vector2(-4, 0);
            var textComp = textGo.AddComponent<Text>();
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 12;
            textComp.color = Color.white;
            textComp.supportRichText = false;

            var input = go.AddComponent<InputField>();
            input.textComponent = textComp;
            input.text = placeholder;
            return input;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private static Dropdown CreateDropdown(Transform parent, string[] options, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Dropdown", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            // Caption text
            var captionGo = new GameObject("Label", typeof(RectTransform));
            captionGo.transform.SetParent(go.transform, false);
            var captionRt = captionGo.GetComponent<RectTransform>();
            captionRt.anchorMin = Vector2.zero;
            captionRt.anchorMax = Vector2.one;
            captionRt.offsetMin = new Vector2(4, 0);
            captionRt.offsetMax = new Vector2(-20, 0);
            var captionText = captionGo.AddComponent<Text>();
            captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            captionText.fontSize = 12;
            captionText.color = Color.white;

            // Template (dropdown list)
            var templateGo = new GameObject("Template", typeof(RectTransform));
            templateGo.transform.SetParent(go.transform, false);
            var templateRt = templateGo.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 0);
            templateRt.anchorMax = new Vector2(1, 0);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = Vector2.zero;
            templateRt.sizeDelta = new Vector2(0, 100);
            var templateBg = templateGo.AddComponent<Image>();
            templateBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var templateScroll = templateGo.AddComponent<ScrollRect>();

            // Viewport
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(templateGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<Image>().color = Color.clear;
            viewportGo.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;

            // Item template
            var itemGo = new GameObject("Item", typeof(RectTransform));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 0.5f);
            itemRt.anchorMax = new Vector2(1, 0.5f);
            itemRt.sizeDelta = new Vector2(0, 22);
            var itemToggle = itemGo.AddComponent<Toggle>();

            var itemBg = itemGo.AddComponent<Image>();
            itemBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var itemLabelGo = new GameObject("Item Label", typeof(RectTransform));
            itemLabelGo.transform.SetParent(itemGo.transform, false);
            var itemLabelRt = itemLabelGo.GetComponent<RectTransform>();
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(4, 0);
            itemLabelRt.offsetMax = new Vector2(-4, 0);
            var itemLabelText = itemLabelGo.AddComponent<Text>();
            itemLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabelText.fontSize = 12;
            itemLabelText.color = Color.white;

            itemToggle.targetGraphic = itemBg;

            templateScroll.content = contentRt;
            templateScroll.viewport = viewportRt;

            templateGo.SetActive(false);

            // Assemble dropdown
            var dropdown = go.AddComponent<Dropdown>();
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabelText;
            dropdown.template = templateRt;

            dropdown.ClearOptions();
            var opts = new System.Collections.Generic.List<Dropdown.OptionData>();
            foreach (var o in options) opts.Add(new Dropdown.OptionData(o));
            dropdown.AddOptions(opts);

            return dropdown;
        }
    }
}
