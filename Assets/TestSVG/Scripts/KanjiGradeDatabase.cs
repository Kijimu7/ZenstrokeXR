using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "KanjiGradeDatabase", menuName = "ZenStrokeXR/Kanji Grade Database")]
public class KanjiGradeDatabase : ScriptableObject
{
    [System.Serializable]
    public class GradeEntry
    {
        public string gradeName;

        [TextArea(3, 10)]
        public string kanjiCharacters;

        [Header("Images are matched by order to the characters above")]
        public List<Sprite> kanjiImages = new List<Sprite>();
    }

    public List<GradeEntry> grades = new List<GradeEntry>();

    public List<string> GetKanjiList(int gradeIndex)
    {
        var result = new List<string>();

        if (gradeIndex < 0 || gradeIndex >= grades.Count)
            return result;

        string raw = grades[gradeIndex].kanjiCharacters;
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (char c in raw)
        {
            if (!char.IsWhiteSpace(c))
                result.Add(c.ToString());
        }

        return result;
    }

    public Sprite GetKanjiImage(int gradeIndex, int kanjiIndex)
    {
        if (gradeIndex < 0 || gradeIndex >= grades.Count)
            return null;

        GradeEntry grade = grades[gradeIndex];
        if (grade == null || grade.kanjiImages == null)
            return null;

        if (kanjiIndex < 0 || kanjiIndex >= grade.kanjiImages.Count)
            return null;

        return grade.kanjiImages[kanjiIndex];
    }

    public Sprite GetKanjiImage(string kanji)
    {
        if (string.IsNullOrWhiteSpace(kanji))
            return null;

        string target = kanji.Trim();

        for (int g = 0; g < grades.Count; g++)
        {
            List<string> kanjiList = GetKanjiList(g);

            for (int i = 0; i < kanjiList.Count; i++)
            {
                if (kanjiList[i] == target)
                    return GetKanjiImage(g, i);
            }
        }

        return null;
    }
}