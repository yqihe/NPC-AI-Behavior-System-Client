using NpcAi.Network;
using NpcAi.UI;
using NpcAi.Visualization;
using UnityEngine;

namespace NpcAi
{
    /// <summary>
    /// Attach to any GameObject in the scene. Bootstraps all required components
    /// so you don't need to wire things up manually in the Inspector.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // NpcClient
            var clientGo = new GameObject("NpcClient");
            var client = clientGo.AddComponent<NpcClient>();

            // NpcManager (parent for all NPC visuals)
            var managerGo = new GameObject("NpcManager");
            managerGo.AddComponent<NpcManager>();

            // GM Panel
            var gmGo = new GameObject("GMPanel");
            gmGo.AddComponent<GMPanel>();

            // Inspector Panel
            var inspGo = new GameObject("NpcInspectorPanel");
            inspGo.AddComponent<NpcInspectorPanel>();

            // Ground plane for map-click raycasting and visual reference
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100, 1, 100); // 1000x1000 units
            var mat = ground.GetComponent<MeshRenderer>().material;
            mat.color = new Color(0.25f, 0.3f, 0.2f);

            // Camera setup
            SetupCamera();
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Top-down angled view
            cam.transform.position = new Vector3(0, 80, -60);
            cam.transform.rotation = Quaternion.Euler(55, 0, 0);
            cam.farClipPlane = 2000f;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            // Add camera controller
            if (cam.GetComponent<CameraController>() == null)
                cam.gameObject.AddComponent<CameraController>();
        }
    }
}
