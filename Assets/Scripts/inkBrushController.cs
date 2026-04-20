
//using UnityEngine;

///// <summary>
///// Connects the Logitech MX Ink stylus to the BrushBristleDeformer.
///// Uses MXInkStylusHandler for pose tracking and tip pressure.
///// 
///// The MX Ink's analog tip force is ideal for calligraphy — it maps directly
///// to bristle bend, splay, and compression for natural brush feel.
///// 
///// Setup:
///// 1. Attach this to the JapaneseCalligraphyBrush root GameObject
///// 2. Assign your MXInkStylusHandler reference
///// 3. Assign the BrushBristleDeformer (auto-finds if on same object)
///// 4. Adjust grip offsets to align the brush model with the physical stylus tip
///// </summary>
//public class MXInkBrushController : MonoBehaviour
//{
//    [Header("References")]
//    [Tooltip("The MX Ink stylus handler providing pose + input")]
//    public MXInkStylusHandler stylusHandler;

//    [Tooltip("The bristle deformer to drive")]
//    public BrushBristleDeformer bristleDeformer;

//    [Header("Pose Alignment")]
//    [Tooltip("Position offset from stylus inking pose to brush origin (local space)")]
//    public Vector3 positionOffset = Vector3.zero;

//    [Tooltip("Rotation offset to align brush model with stylus (Euler degrees)")]
//    public Vector3 rotationOffset = new Vector3(0f, 0f, 0f);

//    [Header("Pressure Mapping")]
//    [Tooltip("Minimum tip force to start deforming bristles (deadzone)")]
//    [Range(0f, 0.2f)]
//    public float tipDeadzone = 0.02f;

//    [Tooltip("Curve to remap tip pressure to deformation amount")]
//    public AnimationCurve pressureCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

//    [Tooltip("Multiplier on final pressure value")]
//    [Range(0.5f, 3f)]
//    public float pressureMultiplier = 1.0f;

//    [Header("Deformation Behavior")]
//    [Tooltip("How quickly deformation responds to pressure changes")]
//    [Range(1f, 30f)]
//    public float deformResponseSpeed = 15f;

//    [Tooltip("How quickly bristles recover when stylus is lifted")]
//    [Range(1f, 20f)]
//    public float recoverySpeed = 10f;

//    [Tooltip("Use raycast for bend direction, or derive from stylus angle")]
//    public BendMode bendMode = BendMode.StylusAngle;

//    [Header("Surface Detection")]
//    [Tooltip("Layers to detect as painting surfaces")]
//    public LayerMask surfaceLayer = ~0;

//    [Tooltip("Raycast distance from tip")]
//    public float raycastDistance = 0.05f;

//    [Header("Haptics")]
//    [Tooltip("Send haptic click on initial surface contact")]
//    public bool hapticOnContact = true;

//    [Tooltip("Continuous haptic buzz proportional to pressure")]
//    public bool hapticOnPressure = false;

//    [Range(0f, 1f)]
//    public float hapticPressureIntensity = 0.1f;

//    [Header("Button Actions")]
//    [Tooltip("Front button resets bristle deformation")]
//    public bool frontButtonResetsDeform = true;

//    [Tooltip("Middle button (squeeze) increases pressure sensitivity")]
//    public bool middleButtonBoostPressure = false;

//    [Range(1f, 3f)]
//    public float middleBoostMultiplier = 1.5f;

//    [Header("Debug")]
//    public bool showDebugGizmos = true;

//    [Header("Controller Visibility")]
//    [Tooltip("OVR controller model to hide when stylus is active (e.g. RightHandAnchor child)")]
//    public GameObject ovrControllerToHide;

//    [Tooltip("The MX Ink 3D model shown by MXInkStylusHandler (will be hidden when brush is active)")]
//    public GameObject mxInkModelToHide;

//    [Tooltip("The calligraphy brush visual root (shown when stylus is active)")]
//    public GameObject brushVisualRoot;

//    private bool _lastStylusActiveState = false;


//    public enum BendMode
//    {
//        StylusAngle,    // Derive bend from stylus tilt angle (fast, no raycast needed)
//        Raycast,        // Use raycast to find surface normal for bend direction
//        Hybrid          // Raycast when touching, stylus angle as fallback
//    }

//    // Shader property IDs
//    private static readonly int _BendAmount = Shader.PropertyToID("_BendAmount");
//    private static readonly int _BendDirection = Shader.PropertyToID("_BendDirection");
//    private static readonly int _SplayAmount = Shader.PropertyToID("_SplayAmount");
//    private static readonly int _PressAmount = Shader.PropertyToID("_PressAmount");
//    private static readonly int _BristleHeight = Shader.PropertyToID("_BristleHeight");

//    // State
//    private float _smoothedPressure;
//    private float _smoothedBend;
//    private float _smoothedSplay;
//    private Vector3 _smoothedBendDir;
//    private MaterialPropertyBlock _propBlock;
//    private bool _wasContacting;
//    private bool _isContacting;
//    private Vector3 _lastVelocity;
//    private Vector3 _lastPosition;

//    // Public state accessors
//    public bool IsContacting => _isContacting;
//    public float CurrentPressure => _smoothedPressure;

//    private void Awake()
//    {
//        _propBlock = new MaterialPropertyBlock();

//        if (bristleDeformer == null)
//            bristleDeformer = GetComponent<BrushBristleDeformer>();
//    }

//private void LateUpdate()
//    {
//        if (stylusHandler == null) return;

//        bool stylusActive = stylusHandler.IsStylusActive;

//        // Toggle visibility every frame (not just on change)
//        // because MXInkStylusHandler.UpdatePose() also toggles controllers each frame
//        if (ovrControllerToHide != null)
//            ovrControllerToHide.SetActive(!stylusActive);

//        if (mxInkModelToHide != null)
//            mxInkModelToHide.SetActive(!stylusActive);

//        if (brushVisualRoot != null)
//        {
//            // Enable all child renderers when active
//            if (stylusActive != _lastStylusActiveState)
//            {
//                var renderers = brushVisualRoot.GetComponentsInChildren<Renderer>(true);
//                foreach (var r in renderers)
//                    r.enabled = stylusActive;
//            }
//        }

//        _lastStylusActiveState = stylusActive;

//        if (!stylusActive) return;

//        UpdatePose();
//        UpdateDeformation();
//        UpdateHaptics();
//        UpdateButtonActions();
//    }

//    private void UpdatePose()
//    {
//        // Follow the MX Ink inking pose
//        Pose inkPose = stylusHandler.InkingPose;

//        Quaternion offsetRot = Quaternion.Euler(rotationOffset);
//        transform.position = inkPose.position + inkPose.rotation * positionOffset;
//        transform.rotation = inkPose.rotation * offsetRot;

//        // Track velocity for motion-based effects
//        _lastVelocity = (transform.position - _lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
//        _lastPosition = transform.position;
//    }

//    private void UpdateDeformation()
//    {
//        if (bristleDeformer == null || bristleDeformer.bristleRenderer == null)
//            return;

//        // Read tip pressure with deadzone and curve
//        float rawTip = stylusHandler.TipValue;
//        float adjustedTip = Mathf.InverseLerp(tipDeadzone, 1f, rawTip);
//        float mappedPressure = pressureCurve.Evaluate(adjustedTip) * pressureMultiplier;

//        // Middle button boost
//        if (middleButtonBoostPressure && stylusHandler.MiddlePressed)
//            mappedPressure *= middleBoostMultiplier;

//        mappedPressure = Mathf.Clamp01(mappedPressure);

//        // Determine bend direction
//        float targetBend = 0f;
//        Vector3 targetBendDir = Vector3.zero;
//        _isContacting = rawTip > tipDeadzone;

//        if (_isContacting)
//        {
//            targetBend = mappedPressure;

//            switch (bendMode)
//            {
//                case BendMode.StylusAngle:
//                    targetBendDir = GetBendFromStylusAngle();
//                    break;

//                case BendMode.Raycast:
//                    targetBendDir = GetBendFromRaycast(mappedPressure, out targetBend);
//                    break;

//                case BendMode.Hybrid:
//                    Vector3 rayBend = GetBendFromRaycast(mappedPressure, out float rayBendAmount);
//                    if (rayBend.sqrMagnitude > 0.001f)
//                    {
//                        targetBendDir = rayBend;
//                        targetBend = rayBendAmount;
//                    }
//                    else
//                    {
//                        targetBendDir = GetBendFromStylusAngle();
//                    }
//                    break;
//            }
//        }

//        // Smooth transitions
//        float speed = _isContacting ? deformResponseSpeed : recoverySpeed;
//        float dt = Time.deltaTime;

//        _smoothedPressure = Mathf.Lerp(_smoothedPressure, mappedPressure, dt * speed);
//        _smoothedBend = Mathf.Lerp(_smoothedBend, targetBend, dt * speed);
//        _smoothedSplay = Mathf.Lerp(_smoothedSplay,
//            _isContacting ? mappedPressure * bristleDeformer.maxSplay : 0f,
//            dt * speed);

//        if (targetBendDir.sqrMagnitude > 0.01f)
//            _smoothedBendDir = Vector3.Lerp(_smoothedBendDir, targetBendDir, dt * speed);

//        // Push to shader
//        bristleDeformer.bristleRenderer.GetPropertyBlock(_propBlock);
//        _propBlock.SetFloat(_BendAmount, _smoothedBend * bristleDeformer.maxBendAngle);
//        _propBlock.SetVector(_BendDirection,
//            transform.InverseTransformDirection(_smoothedBendDir));
//        _propBlock.SetFloat(_SplayAmount, _smoothedSplay);
//        _propBlock.SetFloat(_PressAmount, _smoothedPressure);
//        _propBlock.SetFloat(_BristleHeight, 0.48f);
//        bristleDeformer.bristleRenderer.SetPropertyBlock(_propBlock);

//        _wasContacting = _isContacting;
//    }

//    /// <summary>
//    /// Derive bend direction from stylus tilt relative to world up.
//    /// Fast — no raycast needed. Works well for flat/horizontal surfaces.
//    /// </summary>
//    private Vector3 GetBendFromStylusAngle()
//    {
//        // The stylus forward axis projected onto the horizontal plane
//        // gives us the direction the bristles should bend
//        Vector3 stylusForward = transform.forward; // Tip direction
//        Vector3 projected = Vector3.ProjectOnPlane(stylusForward, Vector3.up);

//        if (projected.sqrMagnitude < 0.001f)
//            return Vector3.zero; // Perfectly vertical — no bend direction

//        return projected.normalized;
//    }

//    /// <summary>
//    /// Use raycast to find surface, derive bend from stylus angle vs surface normal.
//    /// More accurate for non-flat surfaces.
//    /// </summary>
//    private Vector3 GetBendFromRaycast(float pressure, out float bendAmount)
//    {
//        bendAmount = pressure;
//        Vector3 tipDir = transform.forward;
//        Vector3 tipPos = transform.position +
//            tipDir * bristleDeformer.tipOffsetDistance;

//        RaycastHit hit;
//        if (Physics.Raycast(tipPos - tipDir * raycastDistance, tipDir,
//            out hit, raycastDistance * 2f, surfaceLayer))
//        {
//            Vector3 onSurface = Vector3.ProjectOnPlane(tipDir, hit.normal);
//            if (onSurface.sqrMagnitude > 0.001f)
//            {
//                float angle = Vector3.Angle(tipDir, hit.normal);
//                bendAmount = Mathf.Clamp01(angle / 90f) * pressure;
//                return onSurface.normalized;
//            }
//        }

//        // Fallback to stylus angle
//        return GetBendFromStylusAngle();
//    }

//    private void UpdateHaptics()
//    {
//        if (stylusHandler == null) return;

//        // Haptic click on initial contact
//        if (hapticOnContact && _isContacting && !_wasContacting)
//        {
//            stylusHandler.TriggerHapticClick();
//        }

//        // Continuous pressure haptic
//        if (hapticOnPressure && _isContacting && _smoothedPressure > 0.1f)
//        {
//            float amplitude = _smoothedPressure * hapticPressureIntensity;
//            stylusHandler.TriggerHapticPulse(amplitude, Time.deltaTime);
//        }
//    }

//    private void UpdateButtonActions()
//    {
//        if (stylusHandler == null) return;

//        // Front button: reset deformation
//        if (frontButtonResetsDeform && stylusHandler.FrontPressed)
//        {
//            _smoothedPressure = 0f;
//            _smoothedBend = 0f;
//            _smoothedSplay = 0f;
//            _smoothedBendDir = Vector3.zero;
//        }
//    }

//    private void OnDrawGizmos()
//    {
//        if (!showDebugGizmos) return;

//        Vector3 tipDir = transform.forward;

//        // Tip position
//        float tipDist = bristleDeformer != null ? bristleDeformer.tipOffsetDistance : 1.25f;
//        Vector3 tipPos = transform.position + tipDir * tipDist;

//        Gizmos.color = _isContacting ? Color.red : Color.green;
//        Gizmos.DrawWireSphere(tipPos, 0.01f);

//        // Pressure bar
//        if (_smoothedPressure > 0.01f)
//        {
//            Gizmos.color = Color.Lerp(Color.yellow, Color.red, _smoothedPressure);
//            Gizmos.DrawLine(tipPos, tipPos + Vector3.up * _smoothedPressure * 0.1f);
//        }

//        // Bend direction
//        if (_smoothedBendDir.sqrMagnitude > 0.01f)
//        {
//            Gizmos.color = Color.cyan;
//            Gizmos.DrawRay(tipPos, _smoothedBendDir * _smoothedBend * 0.15f);
//        }

//        // Raycast line
//        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
//        Gizmos.DrawRay(tipPos - tipDir * raycastDistance, tipDir * raycastDistance * 2f);
//    }
//}
