using System.Collections.Generic;
using UnityEngine;

public class KanjiStylusTracer : MonoBehaviour
{
    [Header("References")]
    public MXInkStylusHandler stylusHandler;
    public Transform drawingRoot;
    public Transform drawingSurfacePlane;

    [Header("Virtual Paper Contact")]
    public float enterDrawDistance = 0.02f;
    public float exitDrawDistance = 0.035f;
    public float releaseGraceTime = 0.06f;
    public float paperWidth = 0.5f;
    public float paperHeight = 0.7f;


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
    private float lastValidContactTime = -999f;

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
        {
            if (isDrawing)
                EndStroke();
            return;
        }

        if (!TryGetProjectedPoint(out Vector3 projectedWorldPoint, out float surfaceDistance))
        {
            if (isDrawing)
                EndStroke();
            return;
        }

        bool onPaper = IsPointOnPaper(projectedWorldPoint);

        bool shouldStart = onPaper && surfaceDistance <= enterDrawDistance;
        bool shouldContinue = onPaper && surfaceDistance <= exitDrawDistance;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"KanjiStylusTracer | onPaper={onPaper}, surfaceDistance={surfaceDistance:F4}, " +
                $"shouldStart={shouldStart}, shouldContinue={shouldContinue}, isDrawing={isDrawing}"
            );
        }

        if (!isDrawing && shouldStart)
        {
            BeginStroke(projectedWorldPoint);
            lastValidContactTime = Time.time;
        }
        else if (isDrawing && shouldContinue)
        {
            ContinueStroke(projectedWorldPoint);
            lastValidContactTime = Time.time;
        }
        else if (isDrawing)
        {
            bool graceExpired = Time.time - lastValidContactTime > releaseGraceTime;

            if (graceExpired)
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

        float signedDistance = plane.GetDistanceToPoint(tipWorld);
        surfaceDistance = Mathf.Abs(signedDistance);
        projectedWorldPoint = tipWorld - planeNormal * signedDistance;

        Debug.DrawRay(tipWorld, -planeNormal * signedDistance, Color.green);

        return true;
    }

    private bool IsPointOnPaper(Vector3 worldPoint)
    {
        Vector3 local = drawingSurfacePlane.InverseTransformPoint(worldPoint);

        return local.x >= -paperWidth * 0.5f && local.x <= paperWidth * 0.5f &&
               local.y >= -paperHeight * 0.5f && local.y <= paperHeight * 0.5f;
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