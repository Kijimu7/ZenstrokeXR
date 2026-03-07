using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace ZenstrokeXR.Lessons
{
    [Serializable]
    public class LessonDatabase
    {
        [JsonProperty("levels")]
        public List<LessonLevel> Levels;
    }

    [Serializable]
    public class LessonLevel
    {
        [JsonProperty("levelName")]
        public string LevelName;

        [JsonProperty("kanji")]
        public List<KanjiData> Kanji;
    }

    [Serializable]
    public class KanjiData
    {
        [JsonProperty("character")]
        public string Character;

        [JsonProperty("reading_en")]
        public string ReadingEn;

        [JsonProperty("image_key")]
        public string ImageKey;

        [JsonProperty("strokes")]
        public List<List<float[]>> Strokes;

        public List<Vector2> GetStrokePoints(int strokeIndex)
        {
            if (strokeIndex < 0 || strokeIndex >= Strokes.Count)
                return new List<Vector2>();

            var raw = Strokes[strokeIndex];
            var points = new List<Vector2>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i] != null && raw[i].Length >= 2)
                    points.Add(new Vector2(raw[i][0], raw[i][1]));
            }
            return points;
        }

        public int StrokeCount => Strokes?.Count ?? 0;
    }
}
