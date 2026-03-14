using System.Collections;
using UnityEngine;

public class KanjiStrokeAnimator : MonoBehaviour
{
    [SerializeField] private KanjiManager kanjiManager;
    [SerializeField] private KanjiJsonLoader kanjiLoader;
    [SerializeField] private KanjiFontDisplay kanjiFontDisplay;
    [SerializeField] private RectTransform paperArea;
    [SerializeField] private SimpleStrokeDrawer strokeDrawer;

    [SerializeField] private float pointDelay = 0.02f;
    [SerializeField] private float strokeDelay = 0.35f;

    public void PlayAnimation()
    {
        StopAllCoroutines();
        StartCoroutine(AnimateCurrentKanji());
    }

    private void Start()
    {
        PlayAnimation();
    }
    public void ShowNextKanjiAndPlayAnimation()
    {
        StopAllCoroutines();

        if (kanjiManager == null)
        {
            Debug.LogError("KanjiManager is not assigned.");
            return;
        }

        bool moved = kanjiManager.MoveToNextKanji();

        if (!moved)
        {
            Debug.Log("All kanji complete!");
            return;
        }

        if (strokeDrawer != null)
            strokeDrawer.ClearAllStrokes();

        PlayAnimation();
    }

    private IEnumerator AnimateCurrentKanji()
    {
        if (strokeDrawer == null)
        {
            Debug.LogError("StrokeDrawer is not assigned.");
            yield break;
        }

        if (kanjiLoader == null)
        {
            Debug.LogError("KanjiJsonLoader is not assigned.");
            yield break;
        }

        if (kanjiFontDisplay == null)
        {
            Debug.LogError("KanjiFontDisplay is not assigned.");
            yield break;
        }

        if (paperArea == null)
        {
            Debug.LogError("PaperArea is not assigned.");
            yield break;
        }

        strokeDrawer.ClearAllStrokes();

        string currentKanji = kanjiFontDisplay.GetCurrentKanji();
        KanjiEntryData kanjiData = FindKanjiData(currentKanji);

        if (kanjiData == null)
        {
            Debug.LogError($"Kanji animation data not found for '{currentKanji}'.");
            yield break;
        }

        foreach (StrokeData stroke in kanjiData.strokes)
        {
            if (stroke.points == null || stroke.points.Count == 0)
                continue;

            bool firstPoint = true;

            foreach (PointData p in stroke.points)
            {
                Vector2 pos = ConvertJsonPointToPaper(p.x, p.y);

                if (firstPoint)
                {
                    strokeDrawer.BeginStroke(pos);
                    firstPoint = false;
                }
                else
                {
                    strokeDrawer.AddPoint(pos);
                }

                yield return new WaitForSeconds(pointDelay);
            }

            strokeDrawer.EndStroke();
            yield return new WaitForSeconds(strokeDelay);
        }
    }

    private KanjiEntryData FindKanjiData(string character)
    {
        if (kanjiLoader == null || kanjiLoader.database == null || kanjiLoader.database.levels == null)
            return null;

        foreach (var level in kanjiLoader.database.levels)
        {
            if (level.kanji == null)
                continue;

            foreach (var entry in level.kanji)
            {
                if (entry.character == character)
                    return entry;
            }
        }

        return null;
    }

    private Vector2 ConvertJsonPointToPaper(float x, float y)
    {
        Rect rect = paperArea.rect;

        float px = Mathf.Lerp(rect.xMin, rect.xMax, x);
        float flippedY = 1f - y;
        float py = Mathf.Lerp(rect.yMin, rect.yMax, flippedY);

        return new Vector2(px, py);
    }
}