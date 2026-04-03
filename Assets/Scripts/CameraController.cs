using UnityEngine;

namespace NpcAi
{
    /// <summary>
    /// Simple top-down camera controller. WASD/arrows to pan, scroll to zoom.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float panSpeed = 40f;
        [SerializeField] private float zoomSpeed = 20f;
        [SerializeField] private float minY = 10f;
        [SerializeField] private float maxY = 200f;

        private void Update()
        {
            // Pan
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            if (h != 0 || v != 0)
            {
                var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                transform.position += (right * h + forward * v) * panSpeed * Time.unscaledDeltaTime;
            }

            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                var pos = transform.position;
                pos.y = Mathf.Clamp(pos.y - scroll * zoomSpeed, minY, maxY);
                transform.position = pos;
            }
        }
    }
}
