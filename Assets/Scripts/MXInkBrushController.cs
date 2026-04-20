using UnityEngine;

/// <summary>
/// MX Ink Brush Controller
/// Replaces VR hand logic and directly follows MX Ink tracked pose.
/// Works with BrushBristleDeformer for realistic brush behavior.
/// </summary>
public class MXInkBrushController : MonoBehaviour
{
    [Header("Required References")]

    [Tooltip("Assign the REAL tracked MX Ink transform (tip or pose)")]
    public Transform trackedPose;

    [Tooltip("Brush deformation script")]
    public BrushBristleDeformer bristleDeformer;

    [Header("Alignment Offsets")]

    [Tooltip("Fine tune position so brush tip matches MX Ink tip")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Fix orientation mismatch between MX Ink and brush model")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Optional Input (Pressure)")]
    [Range(0f, 1f)]
    public float manualPressure = 0f;

    [Tooltip("Enable if you want trigger/pressure to control brush deformation")]
    public bool useManualPressure = false;

    [Header("Debug")]
    public bool showDebug = true;

    private bool warnedMissingPose = false;

    void Start()
    {
        if (bristleDeformer == null)
            bristleDeformer = GetComponent<BrushBristleDeformer>();
    }

    void LateUpdate()
    {
        UpdateTransform();
        UpdatePressure();
    }

    /// <summary>
    /// Follow MX Ink tracked pose
    /// </summary>
    void UpdateTransform()
    {
        if (trackedPose == null)
        {
            if (!warnedMissingPose && showDebug)
            {
                Debug.LogWarning("[MXInkBrushController] trackedPose is NOT assigned.");
                warnedMissingPose = true;
            }
            return;
        }

        // Position
        transform.position = trackedPose.TransformPoint(positionOffset);

        // Rotation
        transform.rotation = trackedPose.rotation * Quaternion.Euler(rotationOffset);
    }

    /// <summary>
    /// Optional pressure control (trigger or MX Ink tip force)
    /// </summary>
    void UpdatePressure()
    {
        if (!useManualPressure || bristleDeformer == null)
            return;

        // Example bend direction (forward of brush)
        Vector3 bendDir = transform.forward;

        bristleDeformer.SetManualPressure(manualPressure, bendDir);
    }

    /// <summary>
    /// Call this if you wire MX Ink pressure later
    /// </summary>
    public void SetPressure(float pressure)
    {
        manualPressure = Mathf.Clamp01(pressure);
    }
}