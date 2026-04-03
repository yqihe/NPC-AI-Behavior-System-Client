using NpcAi.Network;
using NpcAi.Network.Messages;
using NpcAi.Visualization;
using UnityEngine;
using UnityEngine.UI;

namespace NpcAi.UI
{
    /// <summary>
    /// Click an NPC in the scene to show its detailed state in a floating panel.
    /// Left-click selects; click empty space to deselect.
    /// </summary>
    public class NpcInspectorPanel : MonoBehaviour
    {
        [SerializeField] private NpcClient npcClient;
        [SerializeField] private NpcManager npcManager;

        private GameObject _panel;
        private Text _infoText;
        private NpcVisual _selected;

        private void Start()
        {
            if (npcClient == null) npcClient = FindFirstObjectByType<NpcClient>();
            if (npcManager == null) npcManager = FindFirstObjectByType<NpcManager>();

            BuildPanel();
            _panel.SetActive(false);

            npcClient.OnWorldSnapshot += OnSnapshot;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && Camera.main != null &&
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 1000f))
                {
                    var visual = hit.collider.GetComponent<NpcVisual>();
                    if (visual != null)
                    {
                        Select(visual);
                        return;
                    }
                }
                Deselect();
            }
        }

        private void Select(NpcVisual visual)
        {
            _selected = visual;
            _panel.SetActive(true);
            RefreshInfo(visual.LastSnapshot);

            // Also query server for freshest data
            npcClient.QueryNpc(visual.NpcId, resp =>
            {
                if (_selected != null && _selected.NpcId == resp.npc_id)
                    RefreshInfoFromQuery(resp);
            });
        }

        private void Deselect()
        {
            _selected = null;
            _panel.SetActive(false);
        }

        private void OnSnapshot(WorldSnapshotData snapshot)
        {
            if (_selected == null || snapshot.npcs == null) return;
            foreach (var npc in snapshot.npcs)
            {
                if (npc.npc_id == _selected.NpcId)
                {
                    RefreshInfo(npc);
                    return;
                }
            }
            // NPC removed from snapshot
            Deselect();
        }

        private void RefreshInfo(NpcSnapshot snap)
        {
            _infoText.text =
                $"<b>{snap.npc_id}</b>  ({snap.type_name})\n" +
                $"Position: ({snap.x:F1}, {snap.z:F1})\n" +
                $"FSM State: <b>{snap.fsm_state}</b>\n" +
                $"Action: {snap.current_action}\n" +
                $"Threat: {snap.threat_level:F0}/100";
        }

        private void RefreshInfoFromQuery(QueryNpcResponse resp)
        {
            _infoText.text =
                $"<b>{resp.npc_id}</b>  ({resp.type_name})\n" +
                $"Position: ({resp.x:F1}, {resp.z:F1})\n" +
                $"FSM State: <b>{resp.fsm_state}</b>\n" +
                $"Action: {resp.current_action}\n" +
                $"Threat: {resp.threat_level:F0}/100";
        }

        private void BuildPanel()
        {
            var canvas = FindFirstObjectByType<Canvas>();

            _panel = new GameObject("InspectorPanel", typeof(RectTransform));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1); // top-right
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-10, -10);
            rt.sizeDelta = new Vector2(260, 130);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_panel.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 8);
            textRt.offsetMax = new Vector2(-8, -8);

            _infoText = textGo.AddComponent<Text>();
            _infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _infoText.fontSize = 13;
            _infoText.color = Color.white;
            _infoText.supportRichText = true;
        }
    }
}
