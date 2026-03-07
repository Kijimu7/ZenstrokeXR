using UnityEngine;

namespace ZenstrokeXR.Drawing
{
    /// <summary>
    /// Manages the drawing surface collider and coordinate conversion.
    /// Sits on a Unity Quad mesh (1x1 local units). The parent PaperObject's
    /// scale determines the actual world size (e.g. 0.3 scale = 0.3m surface).
    /// Normalized coords: (0,0) = top-left, (1,1) = bottom-right.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DrawingSurface : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private BoxCollider boxCollider;

        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
            UpdateCollider();
        }

        private void OnValidate()
        {
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
                UpdateCollider();
        }

        private void UpdateCollider()
        {
            // Match the Quad mesh bounds (1x1 in local space)
            // Parent's scale handles actual world size
            boxCollider.size = new Vector3(1f, 1f, 0.002f);
            boxCollider.center = Vector3.zero;
        }

        /// <summary>
        /// Converts a world-space point to normalized [0,1] coordinates.
        /// (0,0) = top-left, (1,1) = bottom-right.
        /// </summary>
        public bool TryGetNormalizedPoint(Vector3 worldPoint, out Vector2 normalized)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);

            // Quad local space: x in [-0.5, 0.5], y in [-0.5, 0.5]
            float nx = local.x + 0.5f;
            float ny = -local.y + 0.5f; // Flip Y: local +Y is up, normalized +Y is down

            normalized = new Vector2(nx, ny);

            return nx >= -0.05f && nx <= 1.05f && ny >= -0.05f && ny <= 1.05f;
        }

        /// <summary>
        /// Converts normalized [0,1] coordinates to world-space position on the surface.
        /// </summary>
        public Vector3 NormalizedToWorld(Vector2 normalized)
        {
            Vector3 local = NormalizedToLocal(normalized);
            return transform.TransformPoint(local);
        }

        /// <summary>
        /// Converts normalized [0,1] to local-space position on the surface.
        /// Since all children of PaperObject share the same transform hierarchy,
        /// these local coords work for any sibling container's LineRenderers.
        /// </summary>
        public Vector3 NormalizedToLocal(Vector2 normalized)
        {
            float localX = normalized.x - 0.5f;
            float localY = -(normalized.y - 0.5f);
            return new Vector3(localX, localY, 0f);
        }

        /// <summary>
        /// Converts a world-space point to this transform's local-space.
        /// </summary>
        public Vector3 WorldToLocal(Vector3 worldPoint)
        {
            return transform.InverseTransformPoint(worldPoint);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(1f, 1f, 0.001f));

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 0.001f));

            // Draw origin marker (top-left in normalized space)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(new Vector3(-0.5f, 0.5f, 0f), 0.02f);
        }
    }
}
