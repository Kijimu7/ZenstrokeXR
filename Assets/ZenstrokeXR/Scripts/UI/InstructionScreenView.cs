using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ZenstrokeXR.Drawing;
using ZenstrokeXR.Lessons;

namespace ZenstrokeXR.UI
{
    public class InstructionScreenView : MonoBehaviour
    {
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI kanjiCharacterText;
        [SerializeField] private TextMeshProUGUI strokeCountText;
        [SerializeField] private TextMeshProUGUI guidanceText;
        [SerializeField] private TextMeshProUGUI levelNameText;

        [Header("Progress Dots")]
        [SerializeField] private Transform progressDotsParent;
        [SerializeField] private GameObject progressDotPrefab;
        [SerializeField] private Color dotInactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color dotActiveColor = new Color(0.3f, 0.6f, 1f, 1f);
        [SerializeField] private Color dotCompletedColor = new Color(0.2f, 0.8f, 0.2f, 1f);

        [Header("Buttons")]
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button autoPlayButton;
        [SerializeField] private Button resetButton;

        [Header("AutoPlay")]
        [SerializeField] private PaperStrokeDrawer paperStrokeDrawer;
        [SerializeField] private float autoPlayInterval = 1.5f;

        [Header("Feedback")]
        [SerializeField] private float feedbackDuration = 1.5f;
        [SerializeField] private Color correctColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color incorrectColor = new Color(0.9f, 0.2f, 0.2f);


        private readonly List<Image> progressDots = new List<Image>();
        private Coroutine feedbackCoroutine;
        private bool isAutoPlaying;

        private void OnEnable()
        {
            SubscribeEvents();

            if (nextButton != null) nextButton.onClick.AddListener(OnNextPressed);
            if (prevButton != null) prevButton.onClick.AddListener(OnPrevPressed);
            if (autoPlayButton != null) autoPlayButton.onClick.AddListener(OnAutoPlayToggled);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetPressed);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (nextButton != null) nextButton.onClick.RemoveListener(OnNextPressed);
            if (prevButton != null) prevButton.onClick.RemoveListener(OnPrevPressed);
            if (autoPlayButton != null) autoPlayButton.onClick.RemoveListener(OnAutoPlayToggled);
            if (resetButton != null) resetButton.onClick.RemoveListener(OnResetPressed);
        }

        private void Start()
        {
            // Re-subscribe in case singleton wasn't ready
            UnsubscribeEvents();
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            var mgr = KanjiLessonManager.Instance;
            if (mgr == null) return;

            mgr.OnLessonLevelChanged += OnLevelChanged;
            //mgr.OnKanjiChanged += OnKanjiChanged;
            mgr.OnStrokeStepChanged += OnStrokeStepChanged;
            mgr.OnValidationResult += OnValidationResult;
            mgr.OnAllStrokesCompleted += OnAllStrokesCompleted;
        }

        private void UnsubscribeEvents()
        {
            var mgr = KanjiLessonManager.Instance;
            if (mgr == null) return;

            mgr.OnLessonLevelChanged -= OnLevelChanged;
            //mgr.OnKanjiChanged -= OnKanjiChanged;
            mgr.OnStrokeStepChanged -= OnStrokeStepChanged;
            mgr.OnValidationResult -= OnValidationResult;
            mgr.OnAllStrokesCompleted -= OnAllStrokesCompleted;
        }

        // ─── Event Handlers ───

        private void OnLevelChanged(LessonLevel level)
        {
            if (levelNameText != null)
                levelNameText.text = level.LevelName;
        }

        //private void OnKanjiChanged(KanjiData kanji)
        //{
        //    if (kanjiCharacterText != null)
        //        kanjiCharacterText.text = kanji.Character;

        //    UpdateStrokeCounter(0, kanji.StrokeCount);
        //    RebuildProgressDots(kanji.StrokeCount);
        //    SetGuidance("Draw the first stroke");
        //}

        private void OnStrokeStepChanged(int strokeIndex)
        {
            var mgr = KanjiLessonManager.Instance;
            if (mgr == null) return;

            UpdateStrokeCounter(strokeIndex, mgr.TotalStrokes);
            UpdateProgressDots(strokeIndex);

            string endingHint = "";
            if (mgr.CurrentKanji != null)
            {
                var ending = mgr.CurrentKanji.GetStrokeEnding(strokeIndex);
                endingHint = ending switch
                {
                    StrokeEndingType.Tome => "\nとめ (Tome) - Stop firmly",
                    StrokeEndingType.Hane => "\nはね (Hane) - Flick at end",
                    StrokeEndingType.Harai => "\nはらい (Harai) - Fade out gradually",
                    _ => ""
                };
            }

            SetGuidance($"Draw stroke {strokeIndex + 1}{endingHint}");
        }

        private void OnValidationResult(bool passed)
        {
            if (passed)
                ShowFeedback("Correct!", correctColor);
            else
                ShowFeedback("Try again", incorrectColor);
        }

        private void OnAllStrokesCompleted()
        {
            ShowFeedback("Complete!", correctColor);
            UpdateProgressDots(int.MaxValue); // All completed

            if (strokeCountText != null)
                strokeCountText.text = "Done!";
        }

        // ─── UI Updates ───

        private void UpdateStrokeCounter(int current, int total)
        {
            if (strokeCountText != null)
                strokeCountText.text = $"Stroke {current + 1} / {total}";
        }

        private void SetGuidance(string text)
        {
            if (guidanceText != null)
                guidanceText.text = text;
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackCoroutine != null)
                StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = StartCoroutine(FeedbackCoroutine(message, color));
        }

        private IEnumerator FeedbackCoroutine(string message, Color color)
        {
            if (guidanceText != null)
            {
                guidanceText.text = message;
                guidanceText.color = color;
            }

            yield return new WaitForSeconds(feedbackDuration);

            if (guidanceText != null)
                guidanceText.color = Color.white;
        }

        // ─── Progress Dots ───

        private void RebuildProgressDots(int count)
        {
            // Clear existing dots
            foreach (var dot in progressDots)
            {
                if (dot != null)
                    Destroy(dot.gameObject);
            }
            progressDots.Clear();

            if (progressDotsParent == null || progressDotPrefab == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(progressDotPrefab, progressDotsParent);
                go.name = $"Dot_{i}";
                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    img.color = dotInactiveColor;
                    progressDots.Add(img);
                }
            }
        }

        private void UpdateProgressDots(int currentIndex)
        {
            for (int i = 0; i < progressDots.Count; i++)
            {
                if (progressDots[i] == null) continue;

                if (i < currentIndex)
                    progressDots[i].color = dotCompletedColor;
                else if (i == currentIndex)
                    progressDots[i].color = dotActiveColor;
                else
                    progressDots[i].color = dotInactiveColor;
            }
        }

        // ─── Button Handlers ───

        private void OnNextPressed()
        {
            if (isAutoPlaying && paperStrokeDrawer != null)
                paperStrokeDrawer.StopStrokeAnimation();
            isAutoPlaying = false;
            UpdateAutoPlayButtonText("Auto");
            KanjiLessonManager.Instance?.NextKanji();
        }

        private void OnPrevPressed()
        {
            if (isAutoPlaying && paperStrokeDrawer != null)
                paperStrokeDrawer.StopStrokeAnimation();
            isAutoPlaying = false;
            UpdateAutoPlayButtonText("Auto");
            KanjiLessonManager.Instance?.PreviousKanji();
        }

        private void OnAutoPlayToggled()
        {
            if (paperStrokeDrawer == null) return;

            if (isAutoPlaying)
            {
                paperStrokeDrawer.StopStrokeAnimation();
                isAutoPlaying = false;
                SetGuidance("Draw the strokes");
                UpdateAutoPlayButtonText("Auto");
            }
            else
            {
                paperStrokeDrawer.PlayStrokeAnimation();
                isAutoPlaying = true;
                SetGuidance("Watch the stroke order...");
                UpdateAutoPlayButtonText("Stop");
            }
        }

        private void OnResetPressed()
        {
            if (isAutoPlaying && paperStrokeDrawer != null)
                paperStrokeDrawer.StopStrokeAnimation();
            isAutoPlaying = false;
            UpdateAutoPlayButtonText("Auto");
            KanjiLessonManager.Instance?.ResetCurrentKanji();
        }

        private void UpdateAutoPlayButtonText(string text)
        {
            if (autoPlayButton != null)
            {
                var txt = autoPlayButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = text;
            }
        }
    }
}
