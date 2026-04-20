
using UnityEngine;

/// <summary>
/// Drives bristle deformation on a calligraphy brush via shader parameters.
/// Attach to the root brush GameObject. Optimized for VR (GPU-driven, no bones).
/// 
/// How it works:
/// - Detects surface contact via a short raycast from the brush tip
/// - Calculates bend direction and amount based on brush angle and pressure
/// - Sends parameters to the bristle shader for vertex displacement
/// - Smooth transitions via lerping for natural feel
/// </summary>

public class BrushBristleDeformer : MonoBehaviour
{
    [Header("Brush Setup")]
    public MeshRenderer bristleRenderer;
    public Vector3 brushTipDirection = Vector3.up;
    public float tipOffsetDistance = 1.25f;

    [Header("Deformation")]
    [Range(0f, 90f)] public float maxBendAngle = 45f;
    [Range(1f, 30f)] public float deformSpeed = 12f;
    [Range(1f, 20f)] public float recoverySpeed = 8f;
    [Range(0f, 1f)] public float maxSplay = 0.4f;

    [Header("Contact Detection")]
    public LayerMask surfaceLayer = ~0;
    public float contactDistance = 0.15f;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool logContactState = false;

    private static readonly int _BendAmount = Shader.PropertyToID("_BendAmount");
    private static readonly int _BendDirection = Shader.PropertyToID("_BendDirection");
    private static readonly int _SplayAmount = Shader.PropertyToID("_SplayAmount");
    private static readonly int _PressAmount = Shader.PropertyToID("_PressAmount");
    private static readonly int _BrushTipWorld = Shader.PropertyToID("_BrushTipWorld");
    private static readonly int _BristleHeight = Shader.PropertyToID("_BristleHeight");

    private float _currentBend;
    private float _currentSplay;
    private float _currentPress;
    private Vector3 _currentBendDir;
    private MaterialPropertyBlock _propBlock;
    private bool _isContacting;
    private bool _lastContacting;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();

        if (bristleRenderer == null)
        {
            var bristle = transform.Find("BristleCurve");
            if (bristle != null)
                bristleRenderer = bristle.GetComponent<MeshRenderer>();
        }

        if (bristleRenderer == null)
        {
            Debug.LogError("[BrushBristleDeformer] No bristleRenderer assigned or found.");
        }
    }

    private void Update()
    {
        if (bristleRenderer == null) return;

        Vector3 tipDir = transform.TransformDirection(brushTipDirection.normalized);
        Vector3 tipPos = transform.position + tipDir * tipOffsetDistance;

        // visible during play in Scene view
            Debug.DrawRay(
            tipPos - tipDir * contactDistance,
            tipDir * contactDistance * 2f,
            Color.yellow,
            0f,
            false
        );

        float targetBend = 0f;
        float targetSplay = 0f;
        float targetPress = 0f;
        Vector3 targetBendDir = Vector3.zero;

        if (Physics.Raycast(
            tipPos - tipDir * contactDistance,
            tipDir,
            out RaycastHit hit,
            contactDistance * 2f,
            surfaceLayer))
        {
            _isContacting = true;

            float penetration = contactDistance - hit.distance + contactDistance;
            targetPress = Mathf.Clamp01(penetration / contactDistance);

            Vector3 surfaceNormal = hit.normal;
            Vector3 brushOnSurface = Vector3.ProjectOnPlane(tipDir, surfaceNormal);

            if (brushOnSurface.sqrMagnitude > 0.001f)
            {
                targetBendDir = brushOnSurface.normalized;
                float angle = Vector3.Angle(tipDir, surfaceNormal);
                targetBend = Mathf.Clamp01(angle / 90f) * targetPress;
            }

            targetSplay = targetPress * maxSplay;
        }
        else
        {
            _isContacting = false;
        }

        if (logContactState && _isContacting != _lastContacting)
        {
            Debug.Log($"[BrushBristleDeformer] Contact changed: {_isContacting}");
            _lastContacting = _isContacting;
        }

        float speed = _isContacting ? deformSpeed : recoverySpeed;
        _currentBend = Mathf.Lerp(_currentBend, targetBend, Time.deltaTime * speed);
        _currentSplay = Mathf.Lerp(_currentSplay, targetSplay, Time.deltaTime * speed);
        _currentPress = Mathf.Lerp(_currentPress, targetPress, Time.deltaTime * speed);

        if (targetBendDir.sqrMagnitude > 0.01f)
            _currentBendDir = Vector3.Lerp(_currentBendDir, targetBendDir, Time.deltaTime * speed);

        bristleRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_BendAmount, _currentBend * maxBendAngle);
        _propBlock.SetVector(_BendDirection, transform.InverseTransformDirection(_currentBendDir));
        _propBlock.SetFloat(_SplayAmount, _currentSplay);
        _propBlock.SetFloat(_PressAmount, _currentPress);
        _propBlock.SetVector(_BrushTipWorld, tipPos);
        _propBlock.SetFloat(_BristleHeight, 0.48f);
        bristleRenderer.SetPropertyBlock(_propBlock);
    }

    public void SetManualPressure(float pressure, Vector3 worldBendDirection)
    {
        _currentPress = pressure;
        _currentBend = pressure;
        _currentSplay = pressure * maxSplay;
        _currentBendDir = worldBendDirection;
    }

    public bool IsContacting => _isContacting;
    public float CurrentPressure => _currentPress;

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 tipDir = transform.TransformDirection(brushTipDirection.normalized);
        Vector3 tipPos = transform.position + tipDir * tipOffsetDistance;

        Gizmos.color = _isContacting ? Color.red : Color.green;
        Gizmos.DrawWireSphere(tipPos, 0.02f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(tipPos - tipDir * contactDistance, tipDir * contactDistance * 2f);

        if (_currentBendDir.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(tipPos, _currentBendDir * _currentBend * 0.3f);
        }
    }
}