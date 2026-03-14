using TMPro;
using UnityEngine;

public class KanjiFontDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI kanjiText;
    [SerializeField] private KanjiJsonLoader kanjiJsonLoader;

    private void Start()
    {
        KanjiEntryData firstKanji = kanjiJsonLoader.GetKanji(0, 0);

        if (firstKanji == null)
        {
            Debug.LogError("First kanji could not be loaded.");
            return;
        }

        ShowKanji(firstKanji.character);
    }

    public void ShowKanji(string character)
    {
        if (kanjiText == null)
        {
            Debug.LogError("Kanji Text is not assigned.");
            return;
        }

        kanjiText.text = character;
    }

    public string GetCurrentKanji()
    {
        if (kanjiText == null)
            return "";

        return kanjiText.text;
    }
}