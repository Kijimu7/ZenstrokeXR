using UnityEngine;

public class MXInkStylusHandler : MonoBehaviour
{
    [SerializeField] private GameObject mxInkModel;
    [SerializeField] private GameObject tip;
    [SerializeField] private GameObject clusterFront;
    [SerializeField] private GameObject clusterMiddle;
    [SerializeField] private GameObject clusterBack;

    [SerializeField] private GameObject leftController;
    [SerializeField] private GameObject rightController;

    public Color activeColor = Color.green;
    public Color doubleTapActiveColor = Color.cyan;
    public Color defaultColor = Color.white;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    [SerializeField] private StylusInputs stylus;

    // Public getters for KanjiStylusTracer
    public bool IsStylusActive => stylus.isActive;
    public float TipValue => stylus.tipValue;
    public Pose InkingPose => stylus.inkingPose;
    public bool FrontPressed => stylus.clusterFrontValue;
    public bool BackPressed => stylus.clusterBackValue;
    public bool MiddlePressed => stylus.clusterMiddleValue > 0.01f;
    public bool IsOnRightHand => stylus.isOnRightHand;

    // Defined action names
    private const string MX_Ink_Pose_Right = "aim_right";
    private const string MX_Ink_Pose_Left = "aim_left";
    private const string MX_Ink_TipForce = "tip";
    private const string MX_Ink_MiddleForce = "middle";
    private const string MX_Ink_ClusterFront = "front";
    private const string MX_Ink_ClusterBack = "back";
    private const string MX_Ink_ClusterBack_DoubleTap = "back_double_tap";
    private const string MX_Ink_ClusterFront_DoubleTap = "front_double_tap";
    private const string MX_Ink_Docked = "docked";
    private const string MX_Ink_Haptic_Pulse = "haptic_pulse";

    private float hapticClickDuration = 0.011f;
    private float hapticClickAmplitude = 1.0f;

    private void Awake()
    {

    }

    private void UpdatePose()
    {
        var leftDevice = OVRPlugin.GetCurrentInteractionProfileName(OVRPlugin.Hand.HandLeft);
        var rightDevice = OVRPlugin.GetCurrentInteractionProfileName(OVRPlugin.Hand.HandRight);

        bool stylusIsOnLeftHand = !string.IsNullOrEmpty(leftDevice) && leftDevice.Contains("logitech");
        bool stylusIsOnRightHand = !string.IsNullOrEmpty(rightDevice) && rightDevice.Contains("logitech");

        stylus.isActive = stylusIsOnLeftHand || stylusIsOnRightHand;
        stylus.isOnRightHand = stylusIsOnRightHand;

        string MX_Ink_Pose = stylus.isOnRightHand ? MX_Ink_Pose_Right : MX_Ink_Pose_Left;

        if (mxInkModel != null)
            mxInkModel.SetActive(stylus.isActive);

        if (rightController != null)
            rightController.SetActive(!stylus.isOnRightHand || !stylus.isActive);

        if (leftController != null)
            leftController.SetActive(stylus.isOnRightHand || !stylus.isActive);

        if (OVRPlugin.GetActionStatePose(MX_Ink_Pose, out OVRPlugin.Posef handPose))
        {
            transform.localPosition = handPose.Position.FromFlippedZVector3f();
            transform.localRotation = handPose.Orientation.FromFlippedZQuatf();

            // IMPORTANT: store WORLD pose for external raycasting
            stylus.inkingPose.position = transform.position;
            stylus.inkingPose.rotation = transform.rotation;
        }
    }

    private void Update()
    {
        OVRInput.Update();
        UpdatePose();

        if (!OVRPlugin.GetActionStateFloat(MX_Ink_TipForce, out stylus.tipValue))
        {
            if (enableDebugLogs)
                Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_TipForce}");
        }

        if (!OVRPlugin.GetActionStateFloat(MX_Ink_MiddleForce, out stylus.clusterMiddleValue))
        {
            if (enableDebugLogs)
                Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_MiddleForce}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterFront, out stylus.clusterFrontValue))
        {
            if (enableDebugLogs)
                Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterFront}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterBack, out stylus.clusterBackValue))
        {
            if (enableDebugLogs)
                Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterBack}");
        }

        // Keep original structure, but fix the target variable names
        OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterBack_DoubleTap, out stylus.clusterBackDoubleTapValue);

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterBack_DoubleTap, out stylus.clusterBackDoubleTapValue))
        {
            if (enableDebugLogs)
                Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterBack_DoubleTap}");
        }

        // Keep dock only if your StylusInputs has this field.
        // If StylusInputs does not have "docked", comment these 4 lines out.
        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_Docked, out stylus.docked))
        {
            if (enableDebugLogs)
                Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_Docked}");
        }

        stylus.any =
        stylus.tipValue > 0 ||
        stylus.clusterFrontValue ||
        stylus.clusterMiddleValue > 0 ||
        stylus.clusterBackValue;

        if (tip != null)
            tip.GetComponent<MeshRenderer>().material.color = stylus.tipValue > 0 ? activeColor : defaultColor;

        if (clusterFront != null)
            clusterFront.GetComponent<MeshRenderer>().material.color = stylus.clusterFrontValue ? activeColor : defaultColor;

        if (clusterMiddle != null)
            clusterMiddle.GetComponent<MeshRenderer>().material.color = stylus.clusterMiddleValue > 0 ? activeColor : defaultColor;

        if (clusterBack != null)
        {
            if (stylus.clusterBackValue)
                clusterBack.GetComponent<MeshRenderer>().material.color = activeColor;
            else
                clusterBack.GetComponent<MeshRenderer>().material.color =
                    stylus.clusterBackDoubleTapValue ? doubleTapActiveColor : defaultColor;
        }


        if (enableDebugLogs)
        {
            Debug.Log(
                $"MXInk | active={stylus.isActive}, rightHand={stylus.isOnRightHand}, " +
                $"tip={stylus.tipValue:F3}, front={stylus.clusterFrontValue}, " +
                $"middle={stylus.clusterMiddleValue:F3}, back={stylus.clusterBackValue}, " +
                $"posePos={stylus.inkingPose.position}, poseRot={stylus.inkingPose.rotation.eulerAngles}"
            );
        }

        // IMPORTANT:
        // We are intentionally NOT calling DrawLine() anymore.
        // KanjiStylusTracer will handle drawing.
    }

    public void TriggerHapticPulse(float amplitude, float duration)
    {
        OVRPlugin.Hand holdingHand = stylus.isOnRightHand ? OVRPlugin.Hand.HandRight : OVRPlugin.Hand.HandLeft;
        OVRPlugin.TriggerVibrationAction(MX_Ink_Haptic_Pulse, holdingHand, duration, amplitude);
    }

    public void TriggerHapticClick()
    {
        TriggerHapticPulse(hapticClickAmplitude, hapticClickDuration);
    }
}