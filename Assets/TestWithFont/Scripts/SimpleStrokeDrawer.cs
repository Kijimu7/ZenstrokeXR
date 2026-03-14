using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimpleStrokeDrawer : MaskableGraphic
{
    [Header("Brush")]
    [SerializeField] private float brushSize = 14f;
    [SerializeField] private float spacing = 3f;

    [Header("Start Taper")]
    [SerializeField] private float startTaperLength = 0.08f;
    [SerializeField] private float startTaperSize = 0.08f;

    [Header("End Taper")]
    [SerializeField] private float endTaperStart = 0.82f;
    [SerializeField] private float endTaperSize = 0.2f;

    [Header("Start Press")]
    [SerializeField] private float startPressSizeMultiplier = 1.8f;
    [SerializeField] private int startPressStampCount = 4;

    [SerializeField] private int flattenedStartStampCount = 6;
    [SerializeField] private int taperedStartStampCount = 6;

    [Header("Hane")]
    [SerializeField] private float haneLiftAmount = 8f;

    [Header("Harai")]
    [SerializeField] private float haraiSweepAmount = 12f;

    [Header("Tome")]
    [SerializeField] private float tomePressSizeMultiplier = 1.12f;
    [SerializeField] private float tomeStopBackAmount = 2f;
    [SerializeField] private float slowStrokeSizeMultiplier = 1.15f;

    private readonly List<Vector2> rawTracePoints = new List<Vector2>();

    public List<Vector2> GetAllDrawnPoints()
    {
        return new List<Vector2>(rawTracePoints);
    }


    public enum StrokeEndingType
    {
        Tome,
        Hane,
        Harai
    }

    [SerializeField] private StrokeEndingType endingType = StrokeEndingType.Tome;

    private readonly List<Vector2> points = new List<Vector2>();
    private readonly List<Vector2> stampedPoints = new List<Vector2>();
    private readonly List<List<Vector2>> completedStrokes = new List<List<Vector2>>();
    public System.Action<List<Vector2>> OnStrokeCompleted;

    public void BeginStroke(Vector2 point)
    {
        points.Clear();

        points.Add(point);
        rawTracePoints.Add(point);

        for (int i = 0; i < startPressStampCount; i++)
        {
            stampedPoints.Add(point);
        }

        SetVerticesDirty();
    }

    public void AddPoint(Vector2 point)
    {
        if (points.Count == 0)
        {
            points.Add(point);
            rawTracePoints.Add(point);
            stampedPoints.Add(point);
            SetVerticesDirty();
            return;
        }

        Vector2 last = points[points.Count - 1];
        float distance = Vector2.Distance(last, point);

        if (distance < 0.5f)
            return;

        points.Add(point);
        rawTracePoints.Add(point);

        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(last, point, t);
            stampedPoints.Add(p);
        }

        SetVerticesDirty();
    }

    public void EndStroke()
    {
        if (points.Count > 0)
        {
            List<Vector2> finishedStroke = new List<Vector2>(points);
            completedStrokes.Add(finishedStroke);

            OnStrokeCompleted?.Invoke(finishedStroke);
        }

        SetVerticesDirty();
    }

    public void ClearCurrentStroke()
    {
        if (completedStrokes.Count > 0)
        {
            completedStrokes.RemoveAt(completedStrokes.Count - 1);
        }

        points.Clear();
        stampedPoints.Clear();
        rawTracePoints.Clear();

        foreach (List<Vector2> stroke in completedStrokes)
        {
            for (int i = 0; i < stroke.Count; i++)
            {
                Vector2 point = stroke[i];

                rawTracePoints.Add(point);
                AddPointToStampedStroke(point, i == 0);
            }
        }

        SetVerticesDirty();
    }

    private void AddPointToStampedStroke(Vector2 point, bool isFirstPoint)
    {
        if (isFirstPoint || stampedPoints.Count == 0)
        {
            stampedPoints.Add(point);
            return;
        }

        Vector2 last = stampedPoints[stampedPoints.Count - 1];
        float distance = Vector2.Distance(last, point);

        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(last, point, t);
            stampedPoints.Add(p);
        }
    }

    public List<Vector2> GetLastCompletedStroke()
    {
        if (completedStrokes.Count == 0)
            return null;

        return completedStrokes[completedStrokes.Count - 1];
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (stampedPoints.Count == 0)
            return;

        for (int i = 0; i < stampedPoints.Count; i++)
        {
            float t = stampedPoints.Count <= 1 ? 1f : i / (float)(stampedPoints.Count - 1);

            float size = brushSize;

            if (i > 0)
            {
                float segmentDistance = Vector2.Distance(stampedPoints[i], stampedPoints[i - 1]);
                float speedFactor = Mathf.Clamp01(segmentDistance / Mathf.Max(0.0001f, spacing));

                float slowBoost = Mathf.Lerp(slowStrokeSizeMultiplier, 1f, speedFactor);
                size *= slowBoost;
            }

            if (endingType == StrokeEndingType.Tome && t > endTaperStart)
            {
                float denom = Mathf.Max(0.0001f, 1f - endTaperStart);
                float endT = (t - endTaperStart) / denom;

                size *= Mathf.Lerp(1f, tomePressSizeMultiplier, endT);
            }

            // Fixed start taper based on stamp count, so it does not change while drawing
            if (i < taperedStartStampCount)
            {
                float startT = taperedStartStampCount <= 1 ? 1f : i / (float)(taperedStartStampCount - 1);
                size *= Mathf.Lerp(startTaperSize, 1f, startT);
            }
            else if (t > endTaperStart)
            {
                float denom = Mathf.Max(0.0001f, 1f - endTaperStart);
                float endT = (t - endTaperStart) / denom;

                switch (endingType)
                {
                    case StrokeEndingType.Tome:
                        // Firm stop, stays thick
                        size *= Mathf.Lerp(1f, 0.92f, endT);
                        break;

                    case StrokeEndingType.Hane:
                        // Flick, thins sharply
                        size *= Mathf.Lerp(1f, 0.25f, endT);
                        break;

                    case StrokeEndingType.Harai:
                        // Sweep, long soft thinning
                        size *= Mathf.Lerp(1f, 0.08f, endT);
                        break;
                }
            }

            bool useFlattenedStartShape = i < flattenedStartStampCount;
            Vector2 drawPoint = stampedPoints[i];
            if (endingType == StrokeEndingType.Hane && t > endTaperStart)
            {
                float denom = Mathf.Max(0.0001f, 1f - endTaperStart);
                float endT = (t - endTaperStart) / denom;

                Vector2 flickDir = Vector2.up;
                Vector2 backDir = Vector2.zero;

                if (i > 0)
                {
                    Vector2 strokeDir = (stampedPoints[i] - stampedPoints[i - 1]).normalized;
                    backDir = (stampedPoints[i - 1] - stampedPoints[i]).normalized;

                    if (strokeDir.sqrMagnitude > 0.0001f)
                    {
                        flickDir = new Vector2(-strokeDir.y, strokeDir.x).normalized;

                        if (flickDir.y < 0f)
                            flickDir = -flickDir;
                    }
                }

                drawPoint += flickDir * (haneLiftAmount * endT);
            }

            AddBrushStamp(vh, drawPoint, size, color, useFlattenedStartShape);
        }
    }

    private void AddBrushStamp(VertexHelper vh, Vector2 center, float size, Color32 color32, bool flattenedStart)
    {
        const int segments = 16;

        float width = size;
        float height = size * 0.55f;

        if (flattenedStart)
        {
            width *= 1.35f;
            height *= 0.70f;
        }

        int centerIndex = vh.currentVertCount;
        vh.AddVert(center, color32, Vector2.zero);

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;

            float x = Mathf.Cos(angle) * (width * 0.5f);
            float y = Mathf.Sin(angle) * (height * 0.5f);

            Vector2 offset = new Vector2(x, y);
            vh.AddVert(center + offset, color32, Vector2.zero);
        }

        for (int i = 1; i <= segments; i++)
        {
            vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
        }
    }


    public void ClearAllStrokes()
    {
        points.Clear();
        stampedPoints.Clear();
        completedStrokes.Clear();
        rawTracePoints.Clear();
        SetVerticesDirty();
    }

    public void SetEndingType(StrokeEndingType newEndingType)
    {
        endingType = newEndingType;
    }

    public int GetCompletedStrokeCount()
    {
        return completedStrokes.Count;
    }

    public List<List<Vector2>> GetCompletedStrokes()
    {
        return new List<List<Vector2>>(completedStrokes);
    }



}