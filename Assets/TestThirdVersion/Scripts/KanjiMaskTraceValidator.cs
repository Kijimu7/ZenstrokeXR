using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KanjiMaskTraceValidator : MonoBehaviour
{
    [SerializeField] private SimpleStrokeDrawer strokeDrawer;
    [SerializeField] private KanjiFontDisplay kanjiFontDisplay;
    [SerializeField] private RectTransform paperArea;
    [SerializeField] private KanjiManager kanjiManager;
    [SerializeField] private TMPro.TextMeshProUGUI resultText;
    [SerializeField] private TMPro.TextMeshProUGUI debugScoreText;
    [SerializeField] private float nextKanjiDelay = 1.0f;
    [SerializeField] private int currentLevelIndex = 0;
    [SerializeField] private int currentKanjiIndex = 0;
    [SerializeField] private KanjiStrokeAnimator kanjiAnimator;
    [SerializeField] private float jsonBoundsPadding = 0f;
    [SerializeField] private float liveRequiredCoverage = 0.70f;
    [SerializeField] private float liveRequiredPrecision = 0.80f;
    [SerializeField] private float completeToNextDelay = 1.0f;
    private Coroutine pendingSuccessCoroutine;

    [Header("Trace Validation")]
    [SerializeField] private float requiredCoverage = 0.60f;
    [SerializeField] private float requiredPrecision = 0.75f;

    public void ValidateTrace()
    {
        Debug.Log("ValidateTrace() called.");
        Debug.Log($"Required coverage: {requiredCoverage}");
        Debug.Log($"Required precision: {requiredPrecision}");

        if (paperArea == null)
        {
            Debug.LogError("Paper Area is not assigned.");
            return;
        }

        if (strokeDrawer == null)
        {
            Debug.LogError("Stroke Drawer is not assigned.");
            return;
        }

        List<Vector2> drawnPoints = strokeDrawer.GetAllDrawnPoints();

        if (drawnPoints == null || drawnPoints.Count == 0)
        {
            Debug.Log("No drawn points found.");

            if (resultText != null)
                resultText.text = "Draw first";

            if (debugScoreText != null)
                debugScoreText.text = "";

            return;
        }

        Rect rect = paperArea.rect;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (Vector2 p in drawnPoints)
        {
            float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, p.x);
            float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, p.y);

            if (normalizedX < minX) minX = normalizedX;
            if (normalizedX > maxX) maxX = normalizedX;
            if (normalizedY < minY) minY = normalizedY;
            if (normalizedY > maxY) maxY = normalizedY;
        }

        Rect userBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        Rect expectedBounds = GetExpectedKanjiBoundsFromJson();

        Debug.Log($"Using JSON bounds: {expectedBounds}");
        Debug.Log($"User trace bounds normalized X[{minX:F2}, {maxX:F2}] Y[{minY:F2}, {maxY:F2}]");

        float overlapScore = CalculateBoundsOverlap(userBounds, expectedBounds);

        float precisionX = 1f - Mathf.Abs(userBounds.xMin - expectedBounds.xMin) - Mathf.Abs(userBounds.xMax - expectedBounds.xMax);
        float precisionY = 1f - Mathf.Abs(userBounds.yMin - expectedBounds.yMin) - Mathf.Abs(userBounds.yMax - expectedBounds.yMax);
        float precisionScore = Mathf.Clamp01((precisionX + precisionY) * 0.5f);

        Debug.Log($"Overlap score: {overlapScore:F3}");
        Debug.Log($"Precision score: {precisionScore:F3}");

        if (debugScoreText != null)
        {
            debugScoreText.text =
                $"Overlap: {overlapScore:F2}\n" +
                $"Precision: {precisionScore:F2}";
        }

        bool pass = overlapScore >= requiredCoverage && precisionScore >= requiredPrecision;

        if (resultText != null)
            resultText.text = pass ? "Complete" : "Try again";

        Debug.Log(pass ? "Trace Complete!" : "Trace Try Again");
        Debug.Log($"Pass result: {pass}");

        if (pass)
        {
            Debug.Log("Starting success coroutine");

            if (pendingSuccessCoroutine != null)
                StopCoroutine(pendingSuccessCoroutine);

            pendingSuccessCoroutine = StartCoroutine(CompleteThenNextCoroutine());
        }
        else
        {
            if (strokeDrawer != null)
                strokeDrawer.ClearAllStrokes();
        }

    }

    private Rect GetExpectedKanjiBoundsFromJson()
    {
        if (kanjiManager == null)
        {
            Debug.LogError("Kanji Manager is not assigned.");
            return new Rect(0.2f, 0.2f, 0.6f, 0.6f);
        }

        KanjiEntryData kanjiData = kanjiManager.GetCurrentKanjiData();

        if (kanjiData == null || kanjiData.strokes == null || kanjiData.strokes.Count == 0)
        {
            Debug.LogError("No kanji data or stroke data found.");
            return new Rect(0.2f, 0.2f, 0.6f, 0.6f);
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (StrokeData stroke in kanjiData.strokes)
        {
            if (stroke.points == null)
                continue;

            foreach (PointData p in stroke.points)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
        }

        minX -= jsonBoundsPadding;
        maxX += jsonBoundsPadding;
        minY -= jsonBoundsPadding;
        maxY += jsonBoundsPadding;

        minX = Mathf.Clamp01(minX);
        maxX = Mathf.Clamp01(maxX);
        minY = Mathf.Clamp01(minY);
        maxY = Mathf.Clamp01(maxY);

        float width = maxX - minX;
        float height = maxY - minY;

        Rect bounds = new Rect(minX, minY, width, height);

        Debug.Log($"Expected kanji bounds from JSON for '{kanjiData.character}': X[{minX:F2}, {maxX:F2}] Y[{minY:F2}, {maxY:F2}]");

        return bounds;
    }

    private float CalculateBoundsOverlap(Rect a, Rect b)
    {
        float overlapMinX = Mathf.Max(a.xMin, b.xMin);
        float overlapMaxX = Mathf.Min(a.xMax, b.xMax);
        float overlapMinY = Mathf.Max(a.yMin, b.yMin);
        float overlapMaxY = Mathf.Min(a.yMax, b.yMax);

        if (overlapMaxX <= overlapMinX || overlapMaxY <= overlapMinY)
            return 0f;

        float overlapArea = (overlapMaxX - overlapMinX) * (overlapMaxY - overlapMinY);
        float targetArea = b.width * b.height;

        if (targetArea <= 0.0001f)
            return 0f;

        return overlapArea / targetArea;
    }

    private void LoadNextKanji()
    {
        if (kanjiManager == null)
        {
            Debug.LogError("KanjiManager is not assigned.");
            return;
        }

        bool moved = kanjiManager.MoveToNextKanji();

        if (!moved)
        {
            if (resultText != null)
                resultText.text = "All kanji complete!";

            Debug.Log("All kanji complete!");
            return;
        }

        if (strokeDrawer != null)
            strokeDrawer.ClearAllStrokes();

        if (debugScoreText != null)
            debugScoreText.text = "";

        if (resultText != null)
            resultText.text = "";
    }

    public bool IsTraceComplete()
    {
        if (paperArea == null || strokeDrawer == null || kanjiManager == null)
            return false;

        List<Vector2> drawnPoints = strokeDrawer.GetAllDrawnPoints();

        if (drawnPoints == null || drawnPoints.Count == 0)
            return false;

        Rect rect = paperArea.rect;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (Vector2 p in drawnPoints)
        {
            float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, p.x);
            float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, p.y);

            if (normalizedX < minX) minX = normalizedX;
            if (normalizedX > maxX) maxX = normalizedX;
            if (normalizedY < minY) minY = normalizedY;
            if (normalizedY > maxY) maxY = normalizedY;
        }

        Rect userBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        Rect expectedBounds = GetExpectedKanjiBoundsFromJson();

        float overlapScore = CalculateBoundsOverlap(userBounds, expectedBounds);

        float precisionX = 1f - Mathf.Abs(userBounds.xMin - expectedBounds.xMin) - Mathf.Abs(userBounds.xMax - expectedBounds.xMax);
        float precisionY = 1f - Mathf.Abs(userBounds.yMin - expectedBounds.yMin) - Mathf.Abs(userBounds.yMax - expectedBounds.yMax);
        float precisionScore = Mathf.Clamp01((precisionX + precisionY) * 0.5f);

        Debug.Log($"Live overlap: {overlapScore:F3}, live precision: {precisionScore:F3}");

        return overlapScore >= liveRequiredCoverage && precisionScore >= liveRequiredPrecision;
    }

    public void CompleteTraceNow()
    {
        if (resultText != null)
            resultText.text = "Complete";

        if (strokeDrawer != null)
            strokeDrawer.ClearAllStrokes();

        if (kanjiAnimator != null)
            kanjiAnimator.ShowNextKanjiAndPlayAnimation();
    }

    public void FailTraceNow()
    {
        if (resultText != null)
            resultText.text = "Try again";

        if (strokeDrawer != null)
            strokeDrawer.ClearAllStrokes();
    }

    public bool IsReadyToValidateByStrokeCount()
    {
        if (kanjiManager == null || strokeDrawer == null)
            return false;

        KanjiEntryData kanjiData = kanjiManager.GetCurrentKanjiData();

        if (kanjiData == null || kanjiData.strokes == null)
            return false;

        int expectedStrokeCount = kanjiData.strokes.Count;
        int userStrokeCount = strokeDrawer.GetCompletedStrokeCount();

        Debug.Log($"User strokes: {userStrokeCount} / Expected strokes: {expectedStrokeCount}");

        return userStrokeCount >= expectedStrokeCount;
    }

    private IEnumerator CompleteThenNextCoroutine()
    {
        Debug.Log("Success delay started");
        Debug.Log($"Waiting for {completeToNextDelay} seconds");

        yield return new WaitForSeconds(completeToNextDelay);

        Debug.Log("Delay finished, moving to next kanji");

        if (strokeDrawer != null)
            strokeDrawer.ClearAllStrokes();

        if (kanjiAnimator != null)
            kanjiAnimator.ShowNextKanjiAndPlayAnimation();
        else
            Debug.LogError("KanjiAnimator is null.");

        pendingSuccessCoroutine = null;
    }




}