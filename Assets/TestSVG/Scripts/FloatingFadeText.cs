using UnityEngine;
using TMPro;
using System.Collections;

public class FloatingFadeText : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI textUI;
    public RectTransform rectTransform;

    [Header("Timing")]
    public float fadeInTime = 0.3f;
    public float visibleTime = 0.6f;
    public float fadeOutTime = 0.5f;

    [Header("Movement")]
    public float moveUpDistance = 40f;

    private Vector2 startPos;
    private Color startColor;

    void Awake()
    {
        if (textUI == null)
            textUI = GetComponent<TextMeshProUGUI>();

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        startPos = rectTransform.anchoredPosition;
        startColor = textUI.color;

        // start invisible
        Color c = textUI.color;
        c.a = 0f;
        textUI.color = c;
    }

    void OnEnable()
    {
        StopAllCoroutines();
        rectTransform.anchoredPosition = startPos;
        StartCoroutine(PlayAnimation());
    }

    IEnumerator PlayAnimation()
    {
        float totalMoveTime = fadeInTime + visibleTime + fadeOutTime;
        float elapsed = 0f;

        while (elapsed < totalMoveTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalMoveTime);

            // move upward
            rectTransform.anchoredPosition = startPos + new Vector2(0f, moveUpDistance * t);

            // alpha control
            float alpha = 1f;

            if (elapsed < fadeInTime)
            {
                alpha = elapsed / fadeInTime;
            }
            else if (elapsed < fadeInTime + visibleTime)
            {
                alpha = 1f;
            }
            else
            {
                float fadeElapsed = elapsed - fadeInTime - visibleTime;
                alpha = 1f - (fadeElapsed / fadeOutTime);
            }

            Color c = startColor;
            c.a = alpha;
            textUI.color = c;

            yield return null;
        }

        // fully invisible at end
        Color endColor = startColor;
        endColor.a = 0f;
        textUI.color = endColor;

        // optional:
        gameObject.SetActive(false);
        // or Destroy(gameObject);
    }

    public void ShowText(string message)
    {
        textUI.text = message;
        gameObject.SetActive(true);
    }
}