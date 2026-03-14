using UnityEngine;

public class KanjiManager : MonoBehaviour
{
    [SerializeField] private KanjiJsonLoader kanjiJsonLoader;
    [SerializeField] private KanjiFontDisplay kanjiFontDisplay;

    [SerializeField] private int currentLevelIndex = 0;
    [SerializeField] private int currentKanjiIndex = 0;

    private void Start()
    {
        ShowCurrentKanji();
    }

    public KanjiEntryData GetCurrentKanjiData()
    {
        if (kanjiJsonLoader == null)
        {
            Debug.LogError("KanjiJsonLoader is not assigned.");
            return null;
        }

        return kanjiJsonLoader.GetKanji(currentLevelIndex, currentKanjiIndex);
    }

    public void ShowCurrentKanji()
    {
        KanjiEntryData currentKanji = GetCurrentKanjiData();

        if (currentKanji == null)
        {
            Debug.LogError("Current kanji data is null.");
            return;
        }

        if (kanjiFontDisplay != null)
            kanjiFontDisplay.ShowKanji(currentKanji.character);
    }

    public bool MoveToNextKanji()
    {
        if (kanjiJsonLoader == null || kanjiJsonLoader.database == null || kanjiJsonLoader.database.levels == null || kanjiJsonLoader.database.levels.Count == 0)
        {
            Debug.LogError("Kanji data is missing.");
            return false;
        }

        var level = kanjiJsonLoader.database.levels[currentLevelIndex];

        if (level.kanji == null || level.kanji.Count == 0)
        {
            Debug.LogError("No kanji in current level.");
            return false;
        }

        currentKanjiIndex++;

        if (currentKanjiIndex >= level.kanji.Count)
        {
            currentKanjiIndex = level.kanji.Count - 1;
            Debug.Log("All kanji complete.");
            return false;
        }

        ShowCurrentKanji();
        return true;
    }

    public int GetCurrentKanjiCount()
    {
        if (kanjiJsonLoader == null || kanjiJsonLoader.database == null || kanjiJsonLoader.database.levels == null || kanjiJsonLoader.database.levels.Count == 0)
            return 0;

        var level = kanjiJsonLoader.database.levels[currentLevelIndex];

        if (level.kanji == null)
            return 0;

        return level.kanji.Count;
    }
}