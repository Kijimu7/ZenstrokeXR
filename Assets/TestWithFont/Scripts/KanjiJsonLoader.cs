using UnityEngine;

public class KanjiJsonLoader : MonoBehaviour
{
    [Header("JSON File")]
    public TextAsset kanjiJsonFile;

    public KanjiLessonDatabase database;

    void Awake()
    {
        LoadKanjiData();

        KanjiEntryData testKanji = GetKanji(0, 0);
        Debug.Log("Loaded kanji: " + testKanji.character);
    }

    void LoadKanjiData()
    {
        if (kanjiJsonFile == null)
        {
            Debug.LogError("Kanji JSON file is not assigned.");
            return;
        }

        database = JsonUtility.FromJson<KanjiLessonDatabase>(kanjiJsonFile.text);

        if (database == null)
        {
            Debug.LogError("Failed to parse JSON.");
            return;
        }

        Debug.Log("Kanji JSON loaded successfully!");
    }

    public KanjiEntryData GetKanji(int levelIndex, int kanjiIndex)
    {
        if (database == null)
        {
            Debug.LogError("Database is null.");
            return null;
        }

        if (levelIndex < 0 || levelIndex >= database.levels.Count)
        {
            Debug.LogError($"Invalid levelIndex: {levelIndex}");
            return null;
        }

        if (kanjiIndex < 0 || kanjiIndex >= database.levels[levelIndex].kanji.Count)
        {
            Debug.LogError($"Invalid kanjiIndex: {kanjiIndex}");
            return null;
        }

        return database.levels[levelIndex].kanji[kanjiIndex];
    }
}