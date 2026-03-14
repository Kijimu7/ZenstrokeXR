using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class KanjiStrokeValidator : MonoBehaviour
{
    [SerializeField] private SimpleStrokeDrawer strokeDrawer;
    [SerializeField] private KanjiJsonLoader kanjiJsonLoader;
    [SerializeField] private float maxAllowedError = 0.18f;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI strokeProgressText;
    private int currentKanjiIndex = 0;

    private int currentStrokeIndex = 0;
    private KanjiEntryData currentKanji;
    [SerializeField] private KanjiFontDisplay kanjiFontDisplay;
    [SerializeField] private float maxStartPointDistance = 0.35f;
    [SerializeField] private float regionPadding = 0.18f;
    [SerializeField] private float maxEndPointDistance = 0.35f;

    private void Start()
    {
        currentKanji = kanjiJsonLoader.GetKanji(0, currentKanjiIndex);

        strokeDrawer.OnStrokeCompleted += ValidateStroke;
        UpdateStrokeProgressText();
        UpdateDisplayedKanji();
        LoadCurrentKanji();
   
    }

    void OnDestroy()
    {
        if (strokeDrawer != null)
            strokeDrawer.OnStrokeCompleted -= ValidateStroke;
    }

    private void ValidateStroke(List<Vector2> userStroke)
    {
        if (currentKanji == null)
            return;

        if (currentStrokeIndex >= currentKanji.strokes.Count)
        {
            Debug.Log("All strokes already completed.");
            return;
        }

        StrokeData expectedStroke = currentKanji.strokes[currentStrokeIndex];

        List<Vector2> normalizedUserStroke = NormalizeStroke(userStroke);
        List<Vector2> expectedPoints = ConvertExpectedStroke(expectedStroke);

        Debug.Log($"Expected start: {expectedPoints[0]}  User start: {normalizedUserStroke[0]}");

        bool startOK = IsStartPointValid(normalizedUserStroke, expectedPoints);

        if (!startOK)
        {
            Debug.Log($"Stroke rejected: wrong start position. Max allowed: {maxStartPointDistance:F3}");

            if (resultText != null)
                resultText.text = "Try again";

            strokeDrawer.ClearCurrentStroke();
            return;
        }

        bool endOK = IsEndPointValid(normalizedUserStroke, expectedPoints);

        if (!endOK)
        {
            Debug.Log($"Stroke rejected: wrong end position. Max allowed: {maxEndPointDistance:F3}");

            if (resultText != null)
                resultText.text = "Try again";

            strokeDrawer.ClearCurrentStroke();
            return;
        }

        bool regionOK = IsStrokeInExpectedRegion(normalizedUserStroke, expectedPoints);

        if (!regionOK)
        {
            Debug.Log("Stroke rejected: outside expected region");

            if (resultText != null)
                resultText.text = "Try again";

            strokeDrawer.ClearCurrentStroke();
            return;
        }

        float error = CalculateStrokeError(normalizedUserStroke, expectedPoints);
        float directionSimilarity = CalculateDirectionSimilarity(normalizedUserStroke, expectedPoints);

        Debug.Log($"User finished stroke {currentStrokeIndex + 1}");
        Debug.Log($"Stroke error: {error:F3}");
        Debug.Log($"Direction similarity: {directionSimilarity:F3}");

        bool requireDirectionCheck = IsMostlyStraight(expectedPoints);

        if (error <= maxAllowedError && (!requireDirectionCheck || Mathf.Abs(directionSimilarity) > 0.65f))
        {
            Debug.Log("Correct stroke");

            if (resultText != null)
                resultText.text = "Complete";

            currentStrokeIndex++;

            ApplyCurrentStrokeEnding();

            if (IsKanjiComplete())
            {
                if (resultText != null)
                    resultText.text = "Kanji Complete!";

                currentKanjiIndex++;

                Invoke(nameof(LoadCurrentKanji), 1.2f);
            }

            UpdateStrokeProgressText();
        }
        else
        {
            Debug.Log("Try again");

            if (resultText != null)
                resultText.text = "Try again";

            strokeDrawer.ClearCurrentStroke();
        }
    }

    public bool IsKanjiComplete()
    {
        if (currentKanji == null)
            return false;

        return currentStrokeIndex >= currentKanji.strokes.Count;
    }

    private void UpdateStrokeProgressText()
    {
        if (strokeProgressText == null || currentKanji == null)
            return;

        int totalStrokes = currentKanji.strokes.Count;

        if (currentStrokeIndex >= totalStrokes)
        {
            strokeProgressText.text = "Finished";
            return;
        }

        int currentStrokeNumber = currentStrokeIndex + 1;
        strokeProgressText.text = $"Stroke {currentStrokeNumber} / {totalStrokes}";
    }

    private List<Vector2> NormalizeStroke(List<Vector2> stroke)
    {
        List<Vector2> result = new List<Vector2>();

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var p in stroke)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float width = maxX - minX;
        float height = maxY - minY;

        foreach (var p in stroke)
        {
            float x = (p.x - minX) / width;
            float y = (p.y - minY) / height;

            result.Add(new Vector2(x, y));
        }

        return result;
    }

    private List<Vector2> ConvertExpectedStroke(StrokeData strokeData)
    {
        List<Vector2> result = new List<Vector2>();

        foreach (PointData p in strokeData.points)
        {
            result.Add(new Vector2(p.x, p.y));
        }

        return result;
    }

    private float CalculateStrokeError(List<Vector2> userStroke, List<Vector2> expectedStroke)
    {
        if (userStroke == null || expectedStroke == null || userStroke.Count == 0 || expectedStroke.Count == 0)
            return float.MaxValue;

        int sampleCount = Mathf.Min(userStroke.Count, expectedStroke.Count);
        float totalError = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            int userIndex = Mathf.RoundToInt(i * (userStroke.Count - 1f) / Mathf.Max(1, sampleCount - 1));
            int expectedIndex = Mathf.RoundToInt(i * (expectedStroke.Count - 1f) / Mathf.Max(1, sampleCount - 1));

            totalError += Vector2.Distance(userStroke[userIndex], expectedStroke[expectedIndex]);
        }

        return totalError / sampleCount;
    }

    private void UpdateDisplayedKanji()
    {
        if (kanjiFontDisplay == null || currentKanji == null)
            return;

        kanjiFontDisplay.ShowKanji(currentKanji.character);
    }

    private void LoadCurrentKanji()
    {
        if (kanjiJsonLoader == null || kanjiJsonLoader.database == null)
            return;

        int totalKanji = kanjiJsonLoader.database.levels[0].kanji.Count;

        if (currentKanjiIndex >= totalKanji)
        {
            currentKanjiIndex = totalKanji - 1;

            if (resultText != null)
                resultText.text = "All kanji complete!";

            if (strokeProgressText != null)
                strokeProgressText.text = "Finished";

            return;
        }

        currentKanji = kanjiJsonLoader.GetKanji(0, currentKanjiIndex);
        currentStrokeIndex = 0;

        if (strokeDrawer != null)
            strokeDrawer.ClearAllStrokes();

        UpdateDisplayedKanji();
        UpdateStrokeProgressText();

        if (resultText != null)
            resultText.text = "";

        ApplyCurrentStrokeEnding();
    }

    private void ApplyCurrentStrokeEnding()
    {
        if (strokeDrawer == null || currentKanji == null)
            return;

        if (currentKanji.stroke_endings == null)
            return;

        if (currentStrokeIndex < 0 || currentStrokeIndex >= currentKanji.stroke_endings.Count)
            return;

        string ending = currentKanji.stroke_endings[currentStrokeIndex];

        switch (ending.ToLower())
        {
            case "tome":
                strokeDrawer.SetEndingType(SimpleStrokeDrawer.StrokeEndingType.Tome);
                break;

            case "hane":
                strokeDrawer.SetEndingType(SimpleStrokeDrawer.StrokeEndingType.Hane);
                break;

            case "harai":
                strokeDrawer.SetEndingType(SimpleStrokeDrawer.StrokeEndingType.Harai);
                break;
        }
    }

    private float CalculateDirectionSimilarity(List<Vector2> userStroke, List<Vector2> expectedStroke)
    {
        if (userStroke == null || expectedStroke == null || userStroke.Count < 2 || expectedStroke.Count < 2)
            return -1f;

        Vector2 userDir = (userStroke[userStroke.Count - 1] - userStroke[0]).normalized;
        Vector2 expectedDir = (expectedStroke[expectedStroke.Count - 1] - expectedStroke[0]).normalized;

        if (userDir.sqrMagnitude < 0.0001f || expectedDir.sqrMagnitude < 0.0001f)
            return -1f;

        return Vector2.Dot(userDir, expectedDir);
    }

    private bool IsMostlyStraight(List<Vector2> stroke)
    {
        if (stroke == null || stroke.Count < 2)
            return true;

        float pathLength = 0f;
        for (int i = 1; i < stroke.Count; i++)
        {
            pathLength += Vector2.Distance(stroke[i - 1], stroke[i]);
        }

        float directLength = Vector2.Distance(stroke[0], stroke[stroke.Count - 1]);

        if (pathLength < 0.0001f)
            return true;

        float straightness = directLength / pathLength;

        return straightness > 0.93f;
    }

    private bool IsStartPointValid(List<Vector2> userStroke, List<Vector2> expectedStroke)
    {
        if (userStroke == null || expectedStroke == null || userStroke.Count == 0 || expectedStroke.Count == 0)
            return false;

        Vector2 userStart = userStroke[0];
        Vector2 expectedStart = expectedStroke[0];

        float startDistance = Vector2.Distance(userStart, expectedStart);

        Debug.Log($"Start point distance: {startDistance:F3}");

        return startDistance <= maxStartPointDistance;
    }

    private bool IsStrokeInExpectedRegion(List<Vector2> userStroke, List<Vector2> expectedStroke)
    {
        if (userStroke == null || expectedStroke == null || userStroke.Count == 0 || expectedStroke.Count == 0)
            return false;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (Vector2 p in expectedStroke)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        minX -= regionPadding;
        maxX += regionPadding;
        minY -= regionPadding;
        maxY += regionPadding;

        Debug.Log($"Expected region X[{minX:F2}, {maxX:F2}] Y[{minY:F2}, {maxY:F2}]");

        int insideCount = 0;

        foreach (Vector2 p in userStroke)
        {
            bool inside = p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;

            if (inside)
                insideCount++;
        }

        float insideRatio = insideCount / (float)userStroke.Count;
        Debug.Log($"Region inside ratio: {insideRatio:F3}");

        return insideRatio >= 0.50f;
    }

    private bool IsEndPointValid(List<Vector2> userStroke, List<Vector2> expectedStroke)
    {
        if (userStroke == null || expectedStroke == null || userStroke.Count == 0 || expectedStroke.Count == 0)
            return false;

        Vector2 userEnd = userStroke[userStroke.Count - 1];
        Vector2 expectedEnd = expectedStroke[expectedStroke.Count - 1];

        float endDistance = Vector2.Distance(userEnd, expectedEnd);

        Debug.Log($"Expected end: {expectedEnd}  User end: {userEnd}");
        Debug.Log($"End point distance: {endDistance:F3}");

        return endDistance <= maxEndPointDistance;
    }

}