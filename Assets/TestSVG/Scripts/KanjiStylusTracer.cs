using System.Collections.Generic;
using UnityEngine;

public class KanjiStylusTracer : MonoBehaviour
{
    [Header("References")]
    public MXInkStylusHandler stylusHandler;
    public Transform drawingRoot;
    public Transform drawingSurfacePlane;

    [Header("Input")]
    public float drawPressureThreshold = 0.01f;
    public float maxDistanceToSurface = 0.03f;

    [Header("Line Rendering")]
    public Material lineMaterial;
    public float lineWidth = 0.02f;
    public Color lineColor = Color.white;

    [Header("Sampling")]
    public float minPointDistance = 0.01f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private readonly List<List<Vector3>> userStrokesLocal = new();
    private readonly List<LineRenderer> lineRenderers = new();

    private List<Vector3> currentStrokeLocal;
    private LineRenderer currentLine;
    private bool isDrawing;

    private void Awake()
    {
        if (drawingRoot == null)
            drawingRoot = transform;
    }

    private void Update()
    {
        if (stylusHandler == null)
        {
            Debug.LogWarning("KanjiStylusTracer: stylusHandler is null.");
            return;
        }

        if (drawingRoot == null)
        {
            Debug.LogWarning("KanjiStylusTracer: drawingRoot is null.");
            return;
        }

        if (drawingSurfacePlane == null)
        {
            Debug.LogWarning("KanjiStylusTracer: drawingSurfacePlane is null.");
            return;
        }

        if (!stylusHandler.IsStylusActive)
            return;

        bool drawPressed = stylusHandler.TipValue > drawPressureThreshold;

        if (enableDebugLogs)
        {
            Debug.Log($"KanjiStylusTracer | active={stylusHandler.IsStylusActive}, tip={stylusHandler.TipValue:F3}, drawPressed={drawPressed}");
        }

        if (!TryGetProjectedPoint(out Vector3 projectedWorldPoint, out float surfaceDistance))
        {
            if (isDrawing && !drawPressed)
                EndStroke();

            return;
        }

        bool nearSurface = surfaceDistance <= maxDistanceToSurface;

        if (enableDebugLogs)
        {
            Debug.Log($"KanjiStylusTracer | nearSurface={nearSurface}, surfaceDistance={surfaceDistance:F4}, projected={projectedWorldPoint}");
        }

        if (!isDrawing && drawPressed && nearSurface)
        {
            BeginStroke(projectedWorldPoint);
        }
        else if (isDrawing && drawPressed && nearSurface)
        {
            ContinueStroke(projectedWorldPoint);
        }
        else if (isDrawing && (!drawPressed || !nearSurface))
        {
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

    private bool TryGetProjectedPoint(out Vector3 projectedWorldPoint, out float surfaceDistance)
    {
        Pose pose = stylusHandler.InkingPose;
        Vector3 tipWorld = pose.position;

        Vector3 planeNormal = drawingSurfacePlane.forward;
        Vector3 planePoint = drawingSurfacePlane.position;

        Plane plane = new Plane(planeNormal, planePoint);

        surfaceDistance = Mathf.Abs(plane.GetDistanceToPoint(tipWorld));
        projectedWorldPoint = tipWorld - planeNormal * plane.GetDistanceToPoint(tipWorld);

        Debug.DrawRay(tipWorld, -planeNormal * surfaceDistance, Color.green);

        return true;
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
        currentLine.startWidth = lineWidth;
        currentLine.endWidth = lineWidth;
        currentLine.startColor = lineColor;
        currentLine.endColor = lineColor;
        currentLine.numCapVertices = 2;
        currentLine.numCornerVertices = 2;
        currentLine.alignment = LineAlignment.TransformZ;

        lineRenderers.Add(currentLine);

        AddPoint(worldPoint, true);

        if (enableDebugLogs)
            Debug.Log($"KanjiStylusTracer: BeginStroke at {worldPoint}");
    }

    private void ContinueStroke(Vector3 worldPoint)
    {
        AddPoint(worldPoint, false);
    }

    private void EndStroke()
    {
        isDrawing = false;

        if (currentStrokeLocal != null && currentStrokeLocal.Count >= 2)
        {
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

        if (enableDebugLogs)
            Debug.Log("KanjiStylusTracer: EndStroke");
    }

    private void AddPoint(Vector3 worldPoint, bool forceAdd)
    {
        Vector3 localPoint = drawingRoot.InverseTransformPoint(worldPoint);
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

        if (enableDebugLogs)
            Debug.Log($"KanjiStylusTracer: AddPoint {localPoint}, count={currentStrokeLocal.Count}");
    }

    private Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        return new Material(shader);
    }
}