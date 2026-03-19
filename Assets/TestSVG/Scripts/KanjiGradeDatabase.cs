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
            if (!char.IsWhiteSpace(c) && !result.Contains(c.ToString()))
                result.Add(c.ToString());
        }

        return result;
    }
}
