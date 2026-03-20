using TMPro;
using UnityEngine;

public class KanjiScoreManager : MonoBehaviour
{
    [Header("Score")]
    public int currentScore = 0;
    public int pointsPerKanji = 10;

    [Header("Optional UI")]
    public TMP_Text scoreText;

    void Start()
    {
        RefreshScoreUI();
    }

    public void AddKanjiPoints()
    {
        AddPoints(pointsPerKanji);
    }

    public void AddPoints(int amount)
    {
        currentScore += amount;
        RefreshScoreUI();

        Debug.Log($"Score +{amount}. Current score = {currentScore}");
    }

    public void ResetScore()
    {
        currentScore = 0;
        RefreshScoreUI();

        Debug.Log("Score reset to 0.");
    }

    public int GetScore()
    {
        return currentScore;
    }

    private void RefreshScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Points: {currentScore}";
    }
}