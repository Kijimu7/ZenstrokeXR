using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class KanjiKanjiValidator : MonoBehaviour
{
    [Header("References")]
    public KanjiLessonController lessonController;
    public KanjiVGStrokePlayer templatePlayer;
    public KanjiVGStrokePlayer instructionPlayer;
    public KanjiStylusTracer mouseTracer;


    [Header("MX Ink Click")]
    [Tooltip("Bind this to the MX Ink button you want to use for clicking, such as front button or middle button.")]
    public InputActionReference mxInkClickAction;

    [Tooltip("Optional: require the stylus ray to be hovering a valid UI target before allowing the click.")]
    public bool requireHoverToClick = false;

    [Tooltip("Optional: set true from a ray/hover script if you only want Check to fire while hovering the Check button.")]
    public bool isHoveringCheckButton = false;

    [Header("Feedback UI")]
    public TMP_Text feedbackText;

    [Header("Validation")]
    [Range(8, 128)]
    public int resampleCount = 32;

    [Tooltip("Lower is stricter. Start around 0.18 to 0.28")]
    public float averageDistanceThreshold = 0.22f;

    [Tooltip("Lower is stricter. Checks where strokes start and end")]
    public float startEndDistanceThreshold = 0.30f;

    [Tooltip("Require exact stroke count match")]
    public bool requireExactStrokeCount = true;

    [Header("Rewards")]
    public KanjiScoreManager scoreManager;

    private bool lastMxInkPressed = false;

    private void OnEnable()
    {
        if (mxInkClickAction != null && mxInkClickAction.action != null)
            mxInkClickAction.action.Enable();
    }

    private void OnDisable()
    {
        if (mxInkClickAction != null && mxInkClickAction.action != null)
            mxInkClickAction.action.Disable();
    }

    private void Update()
    {
        HandleMxInkClick();
    }

    private void HandleMxInkClick()
    {
        if (mxInkClickAction == null || mxInkClickAction.action == null)
            return;

        bool pressed = mxInkClickAction.action.IsPressed();

        // rising edge only
        if (pressed && !lastMxInkPressed)
        {
            if (!requireHoverToClick || isHoveringCheckButton)
            {
                CheckCurrentKanji();
            }
        }

        lastMxInkPressed = pressed;
    }

    public void SetHoveringCheckButton(bool hovering)
    {
        isHoveringCheckButton = hovering;
    }

    public void CheckCurrentKanji()
    {
        Debug.Log("test current button");
        if (templatePlayer == null || mouseTracer == null || lessonController == null)
        {
            Debug.LogWarning("KanjiKanjiValidator: Missing references.");
            return;
        }

        List<List<Vector3>> targetStrokes = templatePlayer.GetAllStrokePointsCopy();
        List<List<Vector3>> userStrokes = mouseTracer.GetUserStrokesCopy();

        if (targetStrokes == null || targetStrokes.Count == 0)
        {
            ShowFeedback("Try Again", Color.red);
            Debug.LogWarning("KanjiKanjiValidator: No target strokes found.");
            return;
        }

        if (userStrokes == null || userStrokes.Count == 0)
        {
            ShowFeedback("Try Again", Color.red);
            ClearUserTracing();
            Debug.Log("Try Again: no user strokes found.");
            FailCurrentKanji();
            return;
        }

        if (requireExactStrokeCount && userStrokes.Count != targetStrokes.Count)
        {
            ShowFeedback("Try Again", Color.red);
            ClearUserTracing();
            Debug.Log($"Try Again: expected {targetStrokes.Count} strokes, but user drew {userStrokes.Count}.");
            FailCurrentKanji();
            return;
        }

        int compareCount = Mathf.Min(userStrokes.Count, targetStrokes.Count);

        if (compareCount == 0)
        {
            ShowFeedback("Try Again", Color.red);
            ClearUserTracing();
            Debug.Log("Try Again: nothing to compare.");
            FailCurrentKanji();
            return;
        }

        for (int i = 0; i < compareCount; i++)
        {
            bool passed = ValidateStroke(userStrokes[i], targetStrokes[i], out float avgDist, out float seDist);

            Debug.Log($"Stroke {i + 1}: avg={avgDist:F3}, start/end={seDist:F3}, pass={passed}");

            if (!passed)
            {
                ShowFeedback("Try Again", Color.red);
                ClearUserTracing();
                Debug.Log($"Try Again: stroke {i + 1} did not match well enough.");
                FailCurrentKanji();
                return;
            }
        }

        if (requireExactStrokeCount && compareCount != targetStrokes.Count)
        {
            ShowFeedback("Try Again", Color.red);
            ClearUserTracing();
            Debug.Log("Try Again: incomplete kanji.");
            FailCurrentKanji();
            return;
        }

        Debug.Log("Complete!");
        ShowFeedback("Complete!", Color.green);
        CompleteCurrentKanji();
    }

    public void ClearUserTracing()
    {
        if (mouseTracer != null)
            mouseTracer.ClearAllStrokes();
    }

    private void FailCurrentKanji()
    {
        if (mouseTracer != null)
            mouseTracer.ClearAllStrokes();

        if (instructionPlayer != null)
        {
            instructionPlayer.ShowInstructionOnly();
            instructionPlayer.Play();
        }
    }

    private void CompleteCurrentKanji()
    {
        if (mouseTracer != null)
            mouseTracer.ClearAllStrokes();

        if (scoreManager != null)
            scoreManager.AddKanjiPoints();

        if (lessonController != null)
            lessonController.MarkCurrentKanjiCompleteAndAdvance();
    }

    private bool ValidateStroke(List<Vector3> userStroke, List<Vector3> targetStroke, out float avgDist, out float startEndDist)
    {
        avgDist = 999f;
        startEndDist = 999f;

        if (userStroke == null || targetStroke == null || userStroke.Count < 2 || targetStroke.Count < 2)
            return false;

        List<Vector2> user2 = ToNormalized2D(userStroke, resampleCount);
        List<Vector2> target2 = ToNormalized2D(targetStroke, resampleCount);

        if (user2 == null || target2 == null || user2.Count != target2.Count || user2.Count == 0)
            return false;

        float total = 0f;
        for (int i = 0; i < user2.Count; i++)
            total += Vector2.Distance(user2[i], target2[i]);

        avgDist = total / user2.Count;

        float startDist = Vector2.Distance(user2[0], target2[0]);
        float endDist = Vector2.Distance(user2[user2.Count - 1], target2[target2.Count - 1]);
        startEndDist = (startDist + endDist) * 0.5f;

        return avgDist <= averageDistanceThreshold && startEndDist <= startEndDistanceThreshold;
    }

    private List<Vector2> ToNormalized2D(List<Vector3> input, int sampleCount)
    {
        if (input == null || input.Count < 2)
            return null;

        List<Vector2> pts = new List<Vector2>(input.Count);
        for (int i = 0; i < input.Count; i++)
            pts.Add(new Vector2(input[i].x, input[i].y));

        List<Vector2> resampled = ResampleByArcLength(pts, sampleCount);
        if (resampled == null || resampled.Count == 0)
            return null;

        Vector2 min = resampled[0];
        Vector2 max = resampled[0];

        for (int i = 1; i < resampled.Count; i++)
        {
            min = Vector2.Min(min, resampled[i]);
            max = Vector2.Max(max, resampled[i]);
        }

        Vector2 size = max - min;
        float scale = Mathf.Max(size.x, size.y);

        if (scale < 0.0001f)
            return null;

        Vector2 center = (min + max) * 0.5f;

        for (int i = 0; i < resampled.Count; i++)
            resampled[i] = (resampled[i] - center) / scale;

        return resampled;
    }

    private List<Vector2> ResampleByArcLength(List<Vector2> points, int sampleCount)
    {
        if (points == null || points.Count < 2 || sampleCount < 2)
            return null;

        float totalLength = 0f;
        for (int i = 1; i < points.Count; i++)
            totalLength += Vector2.Distance(points[i - 1], points[i]);

        if (totalLength < 0.0001f)
            return null;

        List<Vector2> result = new List<Vector2>();
        result.Add(points[0]);

        float step = totalLength / (sampleCount - 1);
        float accumulated = 0f;
        float targetDistance = step;

        int segmentIndex = 1;
        Vector2 segmentStart = points[0];
        Vector2 segmentEnd = points[1];

        while (result.Count < sampleCount - 1)
        {
            float segmentLength = Vector2.Distance(segmentStart, segmentEnd);

            if (accumulated + segmentLength >= targetDistance)
            {
                float remain = targetDistance - accumulated;
                float t = segmentLength <= 0.0001f ? 0f : remain / segmentLength;
                Vector2 newPoint = Vector2.Lerp(segmentStart, segmentEnd, t);
                result.Add(newPoint);

                segmentStart = newPoint;
                accumulated = targetDistance;
                targetDistance += step;
            }
            else
            {
                accumulated += segmentLength;
                segmentIndex++;

                if (segmentIndex >= points.Count)
                    break;

                segmentStart = points[segmentIndex - 1];
                segmentEnd = points[segmentIndex];
            }
        }

        result.Add(points[points.Count - 1]);

        while (result.Count < sampleCount)
            result.Add(points[points.Count - 1]);

        if (result.Count > sampleCount)
            result.RemoveRange(sampleCount, result.Count - sampleCount);

        return result;
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null)
            return;

        feedbackText.text = message;
        feedbackText.color = color;
    }
}