using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class KanjiLessonDatabase
{
    public List<KanjiLevelData> levels;
}

[Serializable]
public class KanjiLevelData
{
    public string levelName;
    public List<KanjiEntryData> kanji;
}

[Serializable]
public class KanjiEntryData
{
    public string character;
    public string reading_en;
    public string image_key;
    public bool useFontTemplate;

    public List<StrokeData> strokes;
    public List<string> stroke_endings;
}

[Serializable]
public class StrokeData
{
    public List<PointData> points;
}

[Serializable]
public class PointData
{
    public float x;
    public float y;
}