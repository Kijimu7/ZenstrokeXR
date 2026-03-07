using System;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZenstrokeXR.Lessons
{
    public class KanjiLessonManager : MonoBehaviour
    {
        public static KanjiLessonManager Instance { get; private set; }

        [Header("Data")]
        [SerializeField] private string jsonResourcePath = "Kanji/kanji_lessons";

        [Header("Progression")]
        [SerializeField] private float advanceDelay = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Events
        public event Action<LessonLevel> OnLessonLevelChanged;
        public event Action<KanjiData> OnKanjiChanged;
        public event Action<int> OnStrokeStepChanged;
        public event Action<bool> OnValidationResult;
        public event Action OnAllStrokesCompleted;
        public event Action OnLessonCompleted;

        // State
        private LessonDatabase database;
        private int currentLevelIndex;
        private int currentKanjiIndex;
        private int currentStrokeIndex;

        // Properties
        public LessonDatabase Database => database;
        public LessonLevel CurrentLevel => database?.Levels?[currentLevelIndex];
        public KanjiData CurrentKanji => CurrentLevel?.Kanji?[currentKanjiIndex];
        public int CurrentStrokeIndex => currentStrokeIndex;
        public int CurrentKanjiIndex => currentKanjiIndex;
        public int CurrentLevelIndex => currentLevelIndex;
        public int TotalStrokes => CurrentKanji?.StrokeCount ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadDatabase();
        }

        private void Start()
        {
            LoadProgress();
            BroadcastCurrentState();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame)
            {
                Log("Reset triggered by keyboard");
                ResetCurrentKanji();
            }
            if (kb.nKey.wasPressedThisFrame)
            {
                Log("Next kanji triggered by keyboard");
                NextKanji();
            }
            if (kb.pKey.wasPressedThisFrame)
            {
                Log("Previous kanji triggered by keyboard");
                PreviousKanji();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LoadDatabase()
        {
            var textAsset = Resources.Load<TextAsset>(jsonResourcePath);
            if (textAsset == null)
            {
                Debug.LogError($"[KanjiLessonManager] Failed to load JSON at Resources/{jsonResourcePath}");
                return;
            }

            database = JsonConvert.DeserializeObject<LessonDatabase>(textAsset.text);
            if (database?.Levels == null || database.Levels.Count == 0)
            {
                Debug.LogError("[KanjiLessonManager] JSON deserialized but contains no levels");
                return;
            }

            int totalKanji = 0;
            foreach (var level in database.Levels)
                totalKanji += level.Kanji?.Count ?? 0;

            Log($"Loaded {database.Levels.Count} levels with {totalKanji} total kanji");
        }

        public void BroadcastCurrentState()
        {
            if (database == null) return;

            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, database.Levels.Count - 1);
            currentKanjiIndex = Mathf.Clamp(currentKanjiIndex, 0, CurrentLevel.Kanji.Count - 1);
            currentStrokeIndex = Mathf.Clamp(currentStrokeIndex, 0, Mathf.Max(0, TotalStrokes - 1));

            OnLessonLevelChanged?.Invoke(CurrentLevel);
            OnKanjiChanged?.Invoke(CurrentKanji);
            OnStrokeStepChanged?.Invoke(currentStrokeIndex);
        }

        public void ReportStrokeResult(bool passed)
        {
            Log($"Stroke {currentStrokeIndex + 1}/{TotalStrokes} result: {(passed ? "PASS" : "FAIL")}");
            OnValidationResult?.Invoke(passed);

            if (passed)
            {
                AdvanceStroke();
            }
        }

        private void AdvanceStroke()
        {
            currentStrokeIndex++;
            SaveProgress();

            if (currentStrokeIndex >= TotalStrokes)
            {
                Log($"All strokes completed for {CurrentKanji.Character}");
                OnAllStrokesCompleted?.Invoke();
                StartCoroutine(AdvanceKanjiAfterDelay());
            }
            else
            {
                OnStrokeStepChanged?.Invoke(currentStrokeIndex);
            }
        }

        private IEnumerator AdvanceKanjiAfterDelay()
        {
            yield return new WaitForSeconds(advanceDelay);
            NextKanji();
        }

        public void NextKanji()
        {
            if (database == null) return;

            currentKanjiIndex++;
            if (currentKanjiIndex >= CurrentLevel.Kanji.Count)
            {
                // Level complete
                currentKanjiIndex = 0;
                currentLevelIndex++;

                if (currentLevelIndex >= database.Levels.Count)
                {
                    currentLevelIndex = database.Levels.Count - 1;
                    currentKanjiIndex = CurrentLevel.Kanji.Count - 1;
                    Log("All levels completed!");
                    OnLessonCompleted?.Invoke();
                    SaveProgress();
                    return;
                }

                Log($"Advanced to level: {CurrentLevel.LevelName}");
                OnLessonLevelChanged?.Invoke(CurrentLevel);
            }

            currentStrokeIndex = 0;
            Log($"Advanced to kanji: {CurrentKanji.Character} ({CurrentKanji.ReadingEn})");
            OnKanjiChanged?.Invoke(CurrentKanji);
            OnStrokeStepChanged?.Invoke(currentStrokeIndex);
            SaveProgress();
        }

        public void PreviousKanji()
        {
            if (database == null) return;

            currentKanjiIndex--;
            if (currentKanjiIndex < 0)
            {
                currentLevelIndex--;
                if (currentLevelIndex < 0)
                {
                    currentLevelIndex = 0;
                    currentKanjiIndex = 0;
                }
                else
                {
                    currentKanjiIndex = CurrentLevel.Kanji.Count - 1;
                    OnLessonLevelChanged?.Invoke(CurrentLevel);
                }
            }

            currentStrokeIndex = 0;
            OnKanjiChanged?.Invoke(CurrentKanji);
            OnStrokeStepChanged?.Invoke(currentStrokeIndex);
            SaveProgress();
        }

        public void SetLevel(int levelIndex)
        {
            if (database == null) return;
            if (levelIndex < 0 || levelIndex >= database.Levels.Count) return;

            currentLevelIndex = levelIndex;
            currentKanjiIndex = 0;
            currentStrokeIndex = 0;

            OnLessonLevelChanged?.Invoke(CurrentLevel);
            OnKanjiChanged?.Invoke(CurrentKanji);
            OnStrokeStepChanged?.Invoke(currentStrokeIndex);
            SaveProgress();
        }

        public void ResetCurrentKanji()
        {
            currentStrokeIndex = 0;
            OnKanjiChanged?.Invoke(CurrentKanji);
            OnStrokeStepChanged?.Invoke(currentStrokeIndex);
        }

        public void SaveProgress()
        {
            PlayerPrefs.SetInt("zxr_level", currentLevelIndex);
            PlayerPrefs.SetInt("zxr_kanji", currentKanjiIndex);
            PlayerPrefs.SetInt("zxr_stroke", currentStrokeIndex);
            PlayerPrefs.Save();
        }

        public void LoadProgress()
        {
            if (database == null) return;

            currentLevelIndex = PlayerPrefs.GetInt("zxr_level", 0);
            currentKanjiIndex = PlayerPrefs.GetInt("zxr_kanji", 0);
            currentStrokeIndex = PlayerPrefs.GetInt("zxr_stroke", 0);

            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, database.Levels.Count - 1);
            currentKanjiIndex = Mathf.Clamp(currentKanjiIndex, 0, CurrentLevel.Kanji.Count - 1);
            currentStrokeIndex = Mathf.Clamp(currentStrokeIndex, 0, Mathf.Max(0, TotalStrokes - 1));

            Log($"Loaded progress: Level {currentLevelIndex}, Kanji {currentKanjiIndex}, Stroke {currentStrokeIndex}");
        }

        private void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[KanjiLessonManager] {msg}");
        }
    }
}
