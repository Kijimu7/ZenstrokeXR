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
        currentLine.startWidth = lineWidth;
        currentLine.endWidth = lineWidth;
        currentLine.startColor = lineColor;
        currentLine.endColor = lineColor;
        currentLine.numCapVertices = 2;
        currentLine.numCornerVertices = 2;
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
}