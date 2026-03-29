using UnityEngine;

public class FudeBrushAnimator : MonoBehaviour
{
    [Header("Target")]
    public Transform targetTip;   // stylus/controller tip target

    [Header("Follow")]
    public float positionLerp = 20f;
    public float rotationLerp = 12f;

    [Header("Brush Tilt")]
    public float maxTiltAngle = 25f;
    public float tiltResponse = 8f;

    [Header("Brush Compression")]
    public Transform brushTipVisual;   // assign the tip mesh or brush root
    public float normalTipLength = 1f;
    public float compressedTipLength = 0.75f;
    public float compressionSpeed = 12f;

    [Header("Input")]
    [Range(0f, 1f)] public float pressure;   // set from stylus input
    public bool isTouchingSurface;

    private Vector3 lastTargetPos;
    private Vector3 velocity;

    void Start()
    {
        if (targetTip != null)
            lastTargetPos = targetTip.position;
    }

    void Update()
    {
        if (targetTip == null) return;

        FollowTarget();
        AnimateTilt();
        AnimateCompression();
    }

    void FollowTarget()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            targetTip.position,
            Time.deltaTime * positionLerp
        );
    }

    void AnimateTilt()
    {
        Vector3 currentTargetPos = targetTip.position;
        velocity = (currentTargetPos - lastTargetPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastTargetPos = currentTargetPos;

        Vector3 localVelocity = transform.parent != null
            ? transform.parent.InverseTransformDirection(velocity)
            : velocity;

        float tiltX = Mathf.Clamp(-localVelocity.z * 0.02f, -1f, 1f) * maxTiltAngle;
        float tiltZ = Mathf.Clamp(localVelocity.x * 0.02f, -1f, 1f) * maxTiltAngle;

        Quaternion targetRot = targetTip.rotation * Quaternion.Euler(tiltX, 0f, tiltZ);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * rotationLerp
        );
    }

    void AnimateCompression()
    {
        if (brushTipVisual == null) return;

        float targetLength = normalTipLength;

        if (isTouchingSurface)
        {
            float t = Mathf.Clamp01(pressure);
            targetLength = Mathf.Lerp(normalTipLength, compressedTipLength, t);
        }

        Vector3 scale = brushTipVisual.localScale;
        scale.y = Mathf.Lerp(scale.y, targetLength, Time.deltaTime * compressionSpeed);
        brushTipVisual.localScale = scale;
    }
}