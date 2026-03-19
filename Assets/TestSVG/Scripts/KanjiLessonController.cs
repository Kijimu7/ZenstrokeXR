using System.Collections.Generic;
using UnityEngine;

public class KanjiLessonController : MonoBehaviour
{
    [Header("References")]
    public KanjiGradeDatabase gradeDatabase;
    // public KanjiVGStrokePlayer strokePlayer;
    public KanjiVGStrokePlayer templatePlayer;
    public KanjiVGStrokePlayer instructionPlayer;

    [Header("Lesson State")]
    [Tooltip("0 = first grade entry in the ScriptableObject")]
    public int currentGradeIndex = 0;

    [Tooltip("0 = first kanji in selected grade")]
    public int currentKanjiIndex = 0;

    [Header("Runtime Debug")]
    public string currentKanjiCharacter;
    public string currentSvgFileName;

    private List<string> currentGradeKanji = new List<string>();

    void Start()
    {
        RefreshGradeList();
        LoadCurrentKanji();
    }

    public void RefreshGradeList()
    {
        if (gradeDatabase == null)
        {
            Debug.LogError("KanjiLessonController: gradeDatabase is not assigned.");
            currentGradeKanji.Clear();
            return;
        }

        currentGradeKanji = gradeDatabase.GetKanjiList(currentGradeIndex);

        if (currentGradeKanji.Count == 0)
        {
            Debug.LogWarning($"KanjiLessonController: No kanji found for grade index {currentGradeIndex}.");
            currentKanjiIndex = 0;
            currentKanjiCharacter = "";
            currentSvgFileName = "";
            return;
        }

        currentKanjiIndex = Mathf.Clamp(currentKanjiIndex, 0, currentGradeKanji.Count - 1);
    }

    public void SetGrade(int newGradeIndex)
    {
        currentGradeIndex = newGradeIndex;
        currentKanjiIndex = 0;
        RefreshGradeList();
        LoadCurrentKanji();
    }

    public void LoadCurrentKanji()
    {
        if (currentGradeKanji == null || currentGradeKanji.Count == 0)
        {
            RefreshGradeList();
            if (currentGradeKanji.Count == 0)
                return;
        }

        if (currentKanjiIndex < 0 || currentKanjiIndex >= currentGradeKanji.Count)
            return;

        currentKanjiCharacter = currentGradeKanji[currentKanjiIndex];
        currentSvgFileName = KanjiToSvgFileName(currentKanjiCharacter);

        Debug.Log($"Loading kanji {currentKanjiCharacter} -> {currentSvgFileName}");

        if (templatePlayer != null)
        {
            templatePlayer.LoadSvgFile(currentSvgFileName);
            templatePlayer.ShowTemplateOnly();
        }

        if (instructionPlayer != null)
        {
            instructionPlayer.LoadSvgFile(currentSvgFileName);
            instructionPlayer.ShowInstructionOnly();
        }
    }


    public void NextKanji()
    {
        if (currentGradeKanji == null || currentGradeKanji.Count == 0)
            return;

        if (currentKanjiIndex < currentGradeKanji.Count - 1)
        {
            currentKanjiIndex++;
            LoadCurrentKanji();
        }
        else
        {
            Debug.Log("Reached end of current grade.");
        }
    }

    public void PreviousKanji()
    {
        if (currentGradeKanji == null || currentGradeKanji.Count == 0)
            return;

        if (currentKanjiIndex > 0)
        {
            currentKanjiIndex--;
            LoadCurrentKanji();
        }
    }

    public void MarkCurrentKanjiCompleteAndAdvance()
    {
        NextKanji();
    }

    public int GetCurrentKanjiCount()
    {
        return currentGradeKanji != null ? currentGradeKanji.Count : 0;
    }

    public static string KanjiToSvgFileName(string kanji)
    {
        if (string.IsNullOrEmpty(kanji))
            return "";

        int codePoint = char.ConvertToUtf32(kanji, 0);
        return codePoint.ToString("x5") + ".svg";
    }
}

