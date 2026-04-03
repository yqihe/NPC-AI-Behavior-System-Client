using NpcAi.Network.Messages;
using UnityEngine;

namespace NpcAi.Visualization
{
    /// <summary>
    /// Attached to each NPC GameObject. Drives position, appearance, and state label.
    /// </summary>
    public class NpcVisual : MonoBehaviour
    {
        public string NpcId { get; private set; }
        public string TypeName { get; private set; }
        public NpcSnapshot LastSnapshot { get; private set; }

        private MeshRenderer _meshRenderer;
        private TextMesh _label;
        private Vector3 _targetPos;

        // Colors per NPC type
        private static readonly Color CivilianColor = new Color(0.2f, 0.6f, 1f);   // blue
        private static readonly Color PoliceColor = new Color(0.2f, 0.4f, 0.8f);     // dark blue

        // Colors per FSM state (applied as emission)
        private static readonly Color IdleEmission = Color.black;
        private static readonly Color AlarmedEmission = new Color(1f, 0.8f, 0f);     // yellow
        private static readonly Color FleeEmission = new Color(1f, 0.4f, 0f);        // orange
        private static readonly Color EngageEmission = new Color(1f, 0.1f, 0.1f);    // red

        public void Init(string npcId, string typeName)
        {
            NpcId = npcId;
            TypeName = typeName;
            gameObject.name = $"NPC_{npcId}";

            // Body — capsule primitive
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(transform);
            body.transform.localPosition = Vector3.up; // half-height offset
            body.transform.localScale = typeName == "police"
                ? new Vector3(0.8f, 1f, 0.8f)
                : new Vector3(0.6f, 0.85f, 0.6f);

            _meshRenderer = body.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = typeName == "police" ? PoliceColor : CivilianColor;
            mat.EnableKeyword("_EMISSION");
            _meshRenderer.material = mat;

            // Remove collider from visual body (we'll put one on root for clicking)
            Destroy(body.GetComponent<Collider>());

            // Root collider for raycasting / click detection
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = Vector3.up;
            col.radius = 0.5f;
            col.height = 2f;

            // Floating label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform);
            labelObj.transform.localPosition = new Vector3(0, 2.5f, 0);
            _label = labelObj.AddComponent<TextMesh>();
            _label.alignment = TextAlignment.Center;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.characterSize = 0.15f;
            _label.fontSize = 48;
            _label.color = Color.white;
        }

        public void ApplySnapshot(NpcSnapshot snap)
        {
            LastSnapshot = snap;
            _targetPos = new Vector3(snap.x, 0f, snap.z);

            // Update label
            _label.text = $"{snap.npc_id}\n<size=36>{snap.fsm_state}</size>";

            // Face label toward camera
            if (Camera.main != null)
            {
                _label.transform.rotation = Quaternion.LookRotation(
                    _label.transform.position - Camera.main.transform.position);
            }

            // Emission by FSM state
            Color emission;
            switch (snap.fsm_state)
            {
                case "Alarmed": emission = AlarmedEmission; break;
                case "Flee":    emission = FleeEmission;    break;
                case "Engage":  emission = EngageEmission;  break;
                default:        emission = IdleEmission;    break;
            }
            _meshRenderer.material.SetColor("_EmissionColor", emission);
        }

        private void Update()
        {
            // Smooth position lerp
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * 8f);
        }
    }
}
