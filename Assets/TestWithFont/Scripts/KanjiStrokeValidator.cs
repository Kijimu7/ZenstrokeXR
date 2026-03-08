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

    private int currentStrokeIndex = 0;
    private KanjiEntryData currentKanji;

    private void Start()
    {
        currentKanji = kanjiJsonLoader.GetKanji(0, 1);

        strokeDrawer.OnStrokeCompleted += ValidateStroke;
        UpdateStrokeProgressText();
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

        float error = CalculateStrokeError(normalizedUserStroke, expectedPoints);

        Debug.Log($"User finished stroke {currentStrokeIndex + 1}");
        Debug.Log($"Stroke error: {error:F3}");

        if (error <= maxAllowedError)
        {
            Debug.Log("Correct stroke");

            if (resultText != null)
                resultText.text = "Complete";

            currentStrokeIndex++;

            if (IsKanjiComplete())
            {
                if (resultText != null)
                    resultText.text = "Kanji Complete!";
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
}