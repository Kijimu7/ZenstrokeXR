using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ZenstrokeXR.Lessons;

namespace ZenstrokeXR.UI
{
    public class PictureDicView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image pictureImage;
        [SerializeField] private TextMeshProUGUI meaningText;
        [SerializeField] private TextMeshProUGUI characterText;

        [Header("Image Loading")]
        [SerializeField] private string imageResourcePrefix = "Kanji/Images/";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
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
            mgr.OnKanjiChanged += OnKanjiChanged;
        }

        private void UnsubscribeEvents()
        {
            var mgr = KanjiLessonManager.Instance;
            if (mgr == null) return;
            mgr.OnKanjiChanged -= OnKanjiChanged;
        }

        private void OnKanjiChanged(KanjiData kanji)
        {
            if (kanji == null) return;

            if (meaningText != null)
                meaningText.text = kanji.ReadingEn;

            if (characterText != null)
                characterText.text = kanji.Character;

            LoadImage(kanji.ImageKey);

            Log($"Updated to: {kanji.Character} ({kanji.ReadingEn})");
        }

        private void LoadImage(string imageKey)
        {
            if (pictureImage == null || string.IsNullOrEmpty(imageKey)) return;

            string path = imageResourcePrefix + imageKey;
            var sprite = Resources.Load<Sprite>(path);

            if (sprite != null)
            {
                pictureImage.sprite = sprite;
                pictureImage.color = Color.white;
            }
            else
            {
                // No image available — show placeholder
                pictureImage.sprite = null;
                pictureImage.color = new Color(0.9f, 0.9f, 0.9f, 0.5f);
                Log($"No sprite found at Resources/{path}");
            }
        }

        private void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[PictureDicView] {msg}");
        }
    }
}
