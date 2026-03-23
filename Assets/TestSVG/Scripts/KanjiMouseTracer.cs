using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class KanjiMouseTracer : MonoBehaviour
{
    [Header("References")]
    public Camera drawCamera;
    public Collider drawingSurfaceCollider;
    public Transform drawingRoot;

    [Header("Line Rendering")]
    public Material lineMaterial;
    public float lineWidth = 0.02f;
    public Color lineColor = Color.white;

    [Header("Sampling")]
    public float minPointDistance = 0.01f;

    [Header("Writing Style")]
    public KanjiWritingStyle writingStyle = KanjiWritingStyle.Normal;

    [Tooltip("How strong the ending effect is")]
    public float endingStrength = 0.08f;

    [Tooltip("How many points at the end to inspect")]
    [Range(2, 12)]
    public int endingSampleCount = 4;

    [Header("Auto Style Detection")]
    public KanjiWritingStyle lastDetectedStyle = KanjiWritingStyle.Normal;

    [Header("Brush Look")]
    public float baseLineWidth = 0.035f;
    public float endTaperWidthMultiplier = 0.15f;
    public float tomeEndWidthMultiplier = 0.45f;
    public float haneEndWidthMultiplier = 0.12f;
    public float haraiEndWidthMultiplier = 0.05f;
    public float midStrokeWidthMultiplier = 1.15f;

    private readonly List<List<Vector3>> userStrokesLocal = new();
    private readonly List<LineRenderer> lineRenderers = new();

    private List<Vector3> currentStrokeLocal;
    private LineRenderer currentLine;
    private bool isDrawing;


    void Awake()
    {
        if (drawingRoot == null)
            drawingRoot = transform;
    }

    void Update()
    {
        if (Mouse.current == null)
        {
            Debug.LogWarning("KanjiMouseTracer: Mouse.current is null.");
            return;
        }


        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (drawCamera == null)
        {
            Debug.LogWarning("KanjiMouseTracer: drawCamera is null.");
            return;
        }

        if (drawingSurfaceCollider == null)
        {
            Debug.LogWarning("KanjiMouseTracer: drawingSurfaceCollider is null.");
            return;
        }

        if (drawingRoot == null)
        {
            Debug.LogWarning("KanjiMouseTracer: drawingRoot is null.");
            return;
        }


        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Mouse pressed.");

            if (TryGetMouseHitPoint(out Vector3 hitWorld))
            {
                Debug.Log($"Hit drawing surface at world point {hitWorld}");
                BeginStroke(hitWorld);
            }
            else
            {
                Debug.Log("Mouse press did not hit drawing surface.");
            }
        }

        if (Mouse.current.leftButton.isPressed && isDrawing)
        {
            if (TryGetMouseHitPoint(out Vector3 hitWorld))
                ContinueStroke(hitWorld);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDrawing)
        {
            Debug.Log("Mouse released, ending stroke.");
            EndStroke();
        }
    }


    public void ClearAllStrokes()
    {
        userStrokesLocal.Clear();

        for (int i = lineRenderers.Count - 1; i >= 0; i--)
        {
            if (lineRenderers[i] != null)
                Destroy(lineRenderers[i].gameObject);
        }

        lineRenderers.Clear();
        currentStrokeLocal = null;
        currentLine = null;
        isDrawing = false;
    }

    public List<List<Vector3>> GetUserStrokesCopy()
    {
        var copy = new List<List<Vector3>>();

        for (int i = 0; i < userStrokesLocal.Count; i++)
            copy.Add(new List<Vector3>(userStrokesLocal[i]));

        return copy;
    }

    private bool TryGetMouseHitPoint(out Vector3 hitWorld)
    {
        Ray ray = drawCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (drawingSurfaceCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            hitWorld = hit.point;
            return true;
        }

        hitWorld = default;
        return false;
    }

    private void BeginStroke(Vector3 worldPoint)
    {
        isDrawing = true;
        currentStrokeLocal = new List<Vector3>();

        GameObject go = new GameObject($"UserStroke_{userStrokesLocal.Count + 1}");
        go.transform.SetParent(drawingRoot, false);

        currentLine = go.AddComponent<LineRenderer>();
        currentLine.useWorldSpace = false;
        currentLine.material = lineMaterial != null ? lineMaterial : CreateDefaultMaterial();
        currentLine.startWidth = baseLineWidth;
        currentLine.endWidth = baseLineWidth;
        currentLine.startColor = lineColor;
        currentLine.endColor = lineColor;
        currentLine.numCapVertices = 4;
        currentLine.numCornerVertices = 4;
        currentLine.alignment = LineAlignment.TransformZ;

        lineRenderers.Add(currentLine);

        AddPoint(worldPoint, forceAdd: true);
    }

    private void ContinueStroke(Vector3 worldPoint)
    {
        AddPoint(worldPoint, forceAdd: false);
    }

    private void EndStroke()
    {
        isDrawing = false;

        if (currentStrokeLocal != null && currentStrokeLocal.Count >= 3)
        {
            lastDetectedStyle = DetectEndingStyle(currentStrokeLocal);
            ApplyBrushWidthByStyle(lastDetectedStyle);

            Debug.Log($"Detected style: {lastDetectedStyle}");

            // OPTIONAL: apply visual enhancement
            ApplyDetectedStyleVisual(lastDetectedStyle);

            currentLine.positionCount = currentStrokeLocal.Count;
            currentLine.SetPositions(currentStrokeLocal.ToArray());

            userStrokesLocal.Add(new List<Vector3>(currentStrokeLocal));
        }
        else
        {
            if (currentLine != null)
                Destroy(currentLine.gameObject);

            if (lineRenderers.Count > 0)
                lineRenderers.RemoveAt(lineRenderers.Count - 1);
        }

        currentStrokeLocal = null;
        currentLine = null;
    }

    private void AddPoint(Vector3 worldPoint, bool forceAdd)
    {
        Vector3 localPoint = drawingRoot.InverseTransformPoint(worldPoint);

        // Push slightly in front of the drawing surface
        localPoint.z = -0.01f;

        if (!forceAdd && currentStrokeLocal.Count > 0)
        {
            float dist = Vector3.Distance(currentStrokeLocal[currentStrokeLocal.Count - 1], localPoint);
            if (dist < minPointDistance)
                return;
        }

        currentStrokeLocal.Add(localPoint);

        currentLine.positionCount = currentStrokeLocal.Count;
        currentLine.SetPositions(currentStrokeLocal.ToArray());
    }

    private Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        return new Material(shader);
    }

    public List<Vector3> GetLastStrokeCopy()
    {
        if (userStrokesLocal.Count == 0)
            return null;

        return new List<Vector3>(userStrokesLocal[userStrokesLocal.Count - 1]);
    }

    public int GetStrokeCount()
    {
        return userStrokesLocal.Count;
    }

    public void RemoveLastStroke()
    {
        if (userStrokesLocal.Count == 0)
            return;

        userStrokesLocal.RemoveAt(userStrokesLocal.Count - 1);

        if (lineRenderers.Count > 0)
        {
            LineRenderer lr = lineRenderers[lineRenderers.Count - 1];
            if (lr != null)
                Destroy(lr.gameObject);

            lineRenderers.RemoveAt(lineRenderers.Count - 1);
        }
    }


   

    private KanjiWritingStyle DetectEndingStyle(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 4)
            return KanjiWritingStyle.Normal;

        int n = pts.Count;

        Vector2 p0 = pts[n - 4];
        Vector2 p1 = pts[n - 3];
        Vector2 p2 = pts[n - 2];
        Vector2 p3 = pts[n - 1];

        Vector2 dir1 = (p1 - p0).normalized;
        Vector2 dir2 = (p3 - p2).normalized;

        float angle = Vector2.Angle(dir1, dir2);
        float endDistance = Vector2.Distance(p2, p3);

        // --- HANE (flick) ---
        if (angle > 45f)
            return KanjiWritingStyle.Hane;

        // --- HARAI (sweep) ---
        if (endDistance > 0.05f)
            return KanjiWritingStyle.Harai;

        // --- TOME (stop) ---
        return KanjiWritingStyle.Tome;
    }

    private void ApplyDetectedStyleVisual(KanjiWritingStyle style)
    {
        switch (style)
        {
            case KanjiWritingStyle.Tome:
                ApplyTomeEnding();
                break;

            case KanjiWritingStyle.Hane:
                ApplyHaneEnding();
                break;

            case KanjiWritingStyle.Harai:
                ApplyHaraiEnding();
                break;
        }
    }

    private void ApplyTomeEnding()
    {
        // Tome = firm stop, tighten the end so it feels like a stop
        int count = currentStrokeLocal.Count;
        int start = Mathf.Max(0, count - endingSampleCount);

        Vector3 anchor = currentStrokeLocal[start];

        for (int i = start + 1; i < count; i++)
        {
            float t = (i - start) / (float)Mathf.Max(1, count - start - 1);
            currentStrokeLocal[i] = Vector3.Lerp(currentStrokeLocal[i], anchor, t * 0.35f);
        }
    }

    private void ApplyHaneEnding()
    {
        // Hane = flick/hook at the end
        int count = currentStrokeLocal.Count;
        if (count < 3)
            return;

        Vector3 p0 = currentStrokeLocal[count - 3];
        Vector3 p1 = currentStrokeLocal[count - 2];
        Vector3 p2 = currentStrokeLocal[count - 1];

        Vector3 dir = (p2 - p1).normalized;
        if (dir.sqrMagnitude < 0.0001f)
            dir = (p1 - p0).normalized;

        // Perpendicular for a hook-like finish
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        Vector3 hookPoint = p2 + (dir * endingStrength * 0.35f) + (perp * endingStrength);

        currentStrokeLocal.Add(hookPoint);
    }

    private void ApplyHaraiEnding()
    {
        // Harai = sweeping release, extend the end smoothly
        int count = currentStrokeLocal.Count;
        if (count < 3)
            return;

        Vector3 p0 = currentStrokeLocal[count - 3];
        Vector3 p1 = currentStrokeLocal[count - 2];
        Vector3 p2 = currentStrokeLocal[count - 1];

        Vector3 dir = (p2 - p1).normalized;
        if (dir.sqrMagnitude < 0.0001f)
            dir = (p1 - p0).normalized;

        Vector3 sweep1 = p2 + dir * (endingStrength * 0.8f);
        Vector3 sweep2 = p2 + dir * (endingStrength * 1.4f);

        currentStrokeLocal.Add(sweep1);
        currentStrokeLocal.Add(sweep2);
    }

    private void ApplyBrushWidthByStyle(KanjiWritingStyle style)
    {
        if (currentLine == null)
            return;

        float endWidthMultiplier = endTaperWidthMultiplier;

        switch (style)
        {
            case KanjiWritingStyle.Tome:
                endWidthMultiplier = tomeEndWidthMultiplier;
                break;

            case KanjiWritingStyle.Hane:
                endWidthMultiplier = haneEndWidthMultiplier;
                break;

            case KanjiWritingStyle.Harai:
                endWidthMultiplier = haraiEndWidthMultiplier;
                break;

            case KanjiWritingStyle.Normal:
            default:
                endWidthMultiplier = endTaperWidthMultiplier;
                break;
        }

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(0.35f, midStrokeWidthMultiplier),
            new Keyframe(0.75f, 0.9f),
            new Keyframe(1f, endWidthMultiplier)
        );

        currentLine.widthCurve = widthCurve;
        currentLine.widthMultiplier = baseLineWidth;
    }
}